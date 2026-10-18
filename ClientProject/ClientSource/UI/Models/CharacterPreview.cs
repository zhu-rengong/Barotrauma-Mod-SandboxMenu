using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Models;

internal enum CharacterPreviewFraming
{
    Body,
    Head
}

internal sealed class CharacterPreview : IDisposable
{
    private const int Size = 128;

    private const int Margin = 27;

    private const int HeadMarginMin = 6;

    private const int FramingCount = 2;

    private const int HeadZoomDivisor = 4;

    private const double PurgeInterval = 1.0;

    private static readonly Dictionary<Character, CharacterPreview?[]> _cache = new(ReferenceEqualityComparer.Instance);
    private static readonly Queue<CharacterPreview> _queued = new();
    private static readonly HashSet<CharacterPreview> _pending = new(ReferenceEqualityComparer.Instance);

    private static SpriteBatch? _batch;
    private static double _purgedAt;

    private readonly Character _character;
    private readonly CharacterPreviewFraming _framing;

    private RenderTarget2D? _target;
    private Camera? _camera;
    private RenderTargetBinding[]? _bindings;
    private long _signature;
    private bool _rendered;

    private CharacterPreview(Character character, CharacterPreviewFraming framing)
    {
        _character = character;
        _framing = framing;
    }

    internal Texture2D? Texture => _target;

    internal static CharacterPreview For(Character character, CharacterPreviewFraming framing = CharacterPreviewFraming.Body)
    {
        if (!_cache.TryGetValue(character, out CharacterPreview?[]? byFraming))
        {
            _cache[character] = byFraming = new CharacterPreview?[FramingCount];
        }

        int index = (int)framing;

        return byFraming[index] ??= new CharacterPreview(character, framing);
    }

    internal static void PurgeDead()
    {
        List<Character>? dead = null;

        foreach (Character character in _cache.Keys)
        {
            if (!character.Removed && Character.CharacterList.Contains(character)) { continue; }

            (dead ??= []).Add(character);
        }

        if (dead is null) { return; }

        foreach (Character character in dead)
        {
            foreach (CharacterPreview? preview in _cache[character])
            {
                if (preview is null) { continue; }

                _pending.Remove(preview);
                preview.Dispose();
            }

            _cache.Remove(character);
        }
    }

    internal static void RenderPending()
    {
        if (_queued.Count == 0) { return; }

        double now = Timing.TotalTime;

        if (now - _purgedAt >= PurgeInterval)
        {
            _purgedAt = now;
            PurgeDead();
        }

        while (_queued.Count > 0)
        {
            CharacterPreview preview = _queued.Dequeue();

            _pending.Remove(preview);

            try
            {
                preview.Render();
            }
            catch (Exception e)
            {
                Services.DebugConsole.AddWarning($"Drawing a character preview failed: {e}");
            }
        }
    }

    internal static void ReleaseAll()
    {
        _queued.Clear();
        _pending.Clear();

        foreach (CharacterPreview?[] byFraming in _cache.Values)
        {
            foreach (CharacterPreview? preview in byFraming) { preview?.Dispose(); }
        }

        _cache.Clear();

        _batch?.Dispose();
        _batch = null;
    }

    internal void Request()
    {
        if (_rendered && Signature() == _signature) { return; }

        if (_pending.Add(this)) { _queued.Enqueue(this); }
    }

    private long Signature()
    {
        if (_character.Removed || _character.AnimController is not { } controller) { return 0L; }

        Limb[] limbs = controller.Limbs;
        long hash = 17L;

        hash = hash * 31L + limbs.Length;
        hash = hash * 31L + (int)_framing;

        for (int i = 0; i < limbs.Length; i++)
        {
            Limb limb = limbs[i];

            if (limb.Alpha > 0f && limb.ActiveSprite is not null) { limb.body.UpdateDrawPosition(true); }

            hash = hash * 31L + BitConverter.SingleToInt32Bits(limb.DrawPosition.X);
            hash = hash * 31L + BitConverter.SingleToInt32Bits(limb.DrawPosition.Y);
            hash = hash * 31L + BitConverter.SingleToInt32Bits(limb.body.DrawRotation);
            hash = hash * 31L + BitConverter.SingleToInt32Bits(limb.Alpha);
            hash = hash * 31L + BitConverter.SingleToInt32Bits(limb.Scale);
            hash = hash * 31L + (limb.ActiveSprite?.GetHashCode() ?? 0);
        }

        return hash;
    }

    public void Dispose()
    {
        _target?.Dispose();
        _target = null;
        _rendered = false;
    }

    private static bool TryBounds(AnimController controller, CharacterInfo? info, CharacterPreviewFraming framing, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;

        foreach (Limb limb in controller.Limbs)
        {
            if (limb.Alpha > 0f && limb.ActiveSprite is not null) { limb.body.UpdateDrawPosition(true); }
        }

        if (framing == CharacterPreviewFraming.Head)
        {
            if (!TryTargetBounds(controller, info, LimbType.Head, ref bounds)
                && !TryTargetBounds(controller, info, LimbType.Torso, ref bounds)
                && !TryTargetBounds(controller, info, null, ref bounds))
            {
                return false;
            }
        }
        else if (!TryTargetBounds(controller, info, null, ref bounds))
        {
            return false;
        }

        int margin = framing == CharacterPreviewFraming.Head
            ? Math.Max(HeadMarginMin, Math.Min(bounds.Width, bounds.Height) / HeadZoomDivisor)
            : Margin;

        bounds.X -= margin;
        bounds.Y -= margin;
        bounds.Width += 2 * margin;
        bounds.Height += 2 * margin;

        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static bool TryTargetBounds(AnimController controller, CharacterInfo? info, LimbType? type, ref Rectangle bounds)
    {
        bool started = false;

        foreach (Limb limb in controller.Limbs)
        {
            if (limb.Alpha <= 0f || limb.ActiveSprite is not { } sprite) { continue; }
            if (limb.IsSevered && limb.type != type) { continue; }
            if (type is { } wanted && limb.type != wanted) { continue; }

            if (limb.type == LimbType.Head && info is not null) { info.CalculateHeadPosition(sprite); }

            if (!started)
            {
                bounds = LimbRect(limb, sprite);
                started = true;

                continue;
            }

            AddPoints(ref bounds, limb, sprite);
        }

        return started;
    }

    private static Rectangle LimbRect(Limb limb, Sprite sprite)
    {
        Rectangle box = new(limb.DrawPosition.ToPoint(), Point.Zero);

        AddPoints(ref box, limb, sprite);

        return box;
    }

    private static bool IsInside(Limb limb, Rectangle view)
        => limb.ActiveSprite is { } sprite && view.Contains(LimbRect(limb, sprite));

    private static void AddPoints(ref Rectangle bounds, Limb limb, Sprite sprite)
    {
        float scale = limb.Scale * limb.TextureScale;
        float extentX = sprite.SourceRect.Width * scale * 0.5f;
        float extentY = sprite.SourceRect.Height * scale * 0.5f;
        Vector2 origin = (sprite.Origin - sprite.SourceRect.Size.ToVector2() * 0.5f) * scale;
        float rotation = limb.body.DrawRotation;
        float sin = MathF.Sin(rotation);
        float cos = MathF.Cos(rotation);

        origin = new Vector2(origin.X * cos + origin.Y * sin, origin.X * sin - origin.Y * cos);

        Point position = limb.DrawPosition.ToPoint();
        Vector2 across = new(extentX * cos, extentX * sin);
        Vector2 along = new(extentY * sin, -extentY * cos);

        AddPoint(ref bounds, position);
        AddPoint(ref bounds, position + (across + along - origin).ToPoint());
        AddPoint(ref bounds, position + (across - along - origin).ToPoint());
        AddPoint(ref bounds, position + (-across - along - origin).ToPoint());
        AddPoint(ref bounds, position + (-across + along - origin).ToPoint());
    }

    private static void AddPoint(ref Rectangle bounds, Point point)
    {
        int left = Math.Min(bounds.X, point.X);
        int top = Math.Min(bounds.Y, point.Y);
        int right = Math.Max(bounds.Right, point.X);
        int bottom = Math.Max(bounds.Bottom, point.Y);

        bounds = new Rectangle(left, top, right - left, bottom - top);
    }

    private void Render()
    {
        if (_character.Removed || _character.AnimController is not { } controller) { return; }
        if (GameMain.Instance?.GraphicsDevice is not { } device) { return; }
        if (!TryBounds(controller, _character.Info, _framing, out Rectangle bounds)) { return; }

        _signature = Signature();
        _rendered = true;

        float zoom = Math.Max(0.01f, Math.Min(Size / (float)bounds.Width, Size / (float)bounds.Height));
        Camera camera = _camera ??= new Camera { AutoUpdateToScreenResolution = false };

        camera.SetResolution(new Point(Size, Size));
        camera.MinZoom = zoom;
        camera.MaxZoom = zoom;
        camera.Zoom = zoom;
        camera.Position = bounds.Center.ToVector2();
        camera.UpdateTransform(false, false);

        _target ??= new RenderTarget2D(device, Size, Size, false, SurfaceFormat.Color, DepthFormat.None);
        _bindings ??= new RenderTargetBinding[1];

        _bindings[0] = default;

        device.GetRenderTargets(_bindings);

        RenderTarget2D? previous = _bindings.Length > 0 ? _bindings[0].RenderTarget as RenderTarget2D : null;
        int visible = Math.Max(bounds.Width, bounds.Height);
        Rectangle view = new(bounds.Center.X - visible / 2, bounds.Center.Y - visible / 2, visible, visible);

        SpriteBatch batch = _batch ??= new SpriteBatch(device);
        Viewport viewport = device.Viewport;

        try
        {
            device.Viewport = new Viewport(0, 0, Size, Size);
            device.SetRenderTarget(_target);
            device.Clear(Color.Transparent);

            batch.Begin(SpriteSortMode.BackToFront, BlendState.NonPremultiplied, null, null, null, null, camera.Transform);

            foreach (Limb limb in controller.Limbs)
            {
                if (limb.IsSevered && !(_framing == CharacterPreviewFraming.Head && limb.type == LimbType.Head)) { continue; }
                if (_framing == CharacterPreviewFraming.Head && !IsInside(limb, view)) { continue; }

                limb.Draw(batch, camera);
            }

            batch.End();
        }
        finally
        {
            device.SetRenderTarget(previous);
            device.Viewport = viewport;
        }
    }
}
