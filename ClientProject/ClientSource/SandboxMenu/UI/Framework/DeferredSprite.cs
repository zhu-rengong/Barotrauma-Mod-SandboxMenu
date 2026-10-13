using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

internal sealed class DeferredSprite : IDisposable
{
    private readonly RectTransform _transform;
    private readonly GUIImage _shadow;
    private readonly GUIImage _image;
    private readonly GUICustomComponent _throbber;
    private readonly bool _fit;
    private Sprite? _sprite;
    private Task? _load;
    private bool _waiting;
    private bool _shadowShown = true;
    private Color _shadowColor = Theme.IconShadow;

    internal DeferredSprite(RectTransform transform, GUIImage.ScalingMode mode)
    {
        _transform = transform;
        _fit = mode != GUIImage.ScalingMode.None;

        _shadow = new GUIImage(
            new RectTransform(Vector2.One, transform, Anchor.Center)
            {
                AbsoluteOffset = new Point(UiMetrics.DipInt(Theme.IconShadowOffset), UiMetrics.DipInt(Theme.IconShadowOffset))
            },
            style: null,
            scaleToFit: mode)
        {
            CanBeFocused = false,

            OverrideState = GUIComponent.ComponentState.None,
            Color = Theme.IconShadow
        };

        _image = new GUIImage(new RectTransform(Vector2.One, transform, Anchor.Center), style: null, scaleToFit: mode)
        {
            CanBeFocused = false,
            OverrideState = GUIComponent.ComponentState.None,
            Color = Color.White
        };

        _throbber = new GUICustomComponent(transform, DrawThrobber, Load)
        {
            CanBeFocused = false,
            AutoUpdate = false
        };

        if (_fit) { _transform.SizeChanged += Fit; }
    }

    internal RectTransform Transform => _transform;

    internal GUIImage Image => _image;

    internal bool Shadow
    {
        get => _shadowShown;
        set
        {
            _shadowShown = value;
            ApplyShadow();
        }
    }

    internal Color ShadowColor
    {
        set
        {
            _shadowColor = value;
            ApplyShadow();
        }
    }

    internal void Refit() => Fit();

    internal void Set(Sprite? sprite)
    {
        if (ReferenceEquals(_sprite, sprite)) { return; }

        _sprite = sprite;
        _load = null;

        if (sprite is null || sprite.Loaded || !sprite.LazyLoad)
        {
            Show(sprite);
            return;
        }

        _image.Sprite = null;
        _shadow.Sprite = null;
        SetWaiting(true);
    }

    private void Load(float deltaTime, GUICustomComponent component)
    {
        if (_sprite is not { } sprite) { return; }

        if (_load is { IsCompleted: true })
        {
            _load = null;

            if (!sprite.Loaded)
            {
                SetWaiting(false);
                return;
            }
        }

        if (sprite.Loaded)
        {
            Show(sprite);
            return;
        }

        if (_load is not null || !DecodeGate.Acquire()) { return; }

        _load = sprite.LazyLoadAsync();
        DecodeGate.Started(_load);
    }

    private void ApplyShadow() => _shadow.Color = _shadowShown ? _shadowColor : Color.Transparent;

    private void SetWaiting(bool waiting)
    {
        _waiting = waiting;
        _throbber.AutoUpdate = waiting;
    }

    private void Show(Sprite? sprite)
    {
        _load = null;
        SetWaiting(false);
        _image.Sprite = sprite;
        _shadow.Sprite = sprite;
        Fit();
    }

    private void Fit()
    {
        if (_image.Sprite is not { } sprite) { return; }

        Rectangle source = sprite.SourceRect;
        Rectangle rect = _image.RectTransform.Rect;
        if (source.Width <= 0 || source.Height <= 0 || rect.Width <= 0 || rect.Height <= 0) { return; }

        _image.Scale = Math.Min((float)rect.Width / source.Width, (float)rect.Height / source.Height);
        _shadow.Scale = _image.Scale;
    }

    private void DrawThrobber(SpriteBatch spriteBatch, GUICustomComponent component)
    {
        if (!_waiting) { return; }

        GUISpriteSheet sheet = GUIStyle.GenericThrobber;
        Vector2 frame = sheet.FrameSize.ToVector2();
        Rectangle rect = component.Rect;
        if (frame.X <= 0f || frame.Y <= 0f || rect.Width <= 0 || rect.Height <= 0) { return; }

        sheet.Draw(
            spriteBatch,
            (int)(Timing.TotalTime * 20.0) % sheet.FrameCount,
            rect.Center.ToVector2(),
            Color.White,
            frame * 0.5f,
            0f,
            rect.Size.ToVector2() / frame,
            SpriteEffects.None);
    }

    public void Dispose()
    {
        if (_fit) { _transform.SizeChanged -= Fit; }

        _sprite = null;
        _load = null;
        _image.Sprite = null;
        _shadow.Sprite = null;
        _waiting = false;
        _throbber.AutoUpdate = false;
        _throbber.OnDraw = null;
    }

    private static class DecodeGate
    {
        private const double StuckSeconds = 8.0;

        private static Task? _current;
        private static double _started;

        internal static bool Acquire()
        {
            if (_current is { IsCompleted: false } && Timing.TotalTime - _started < StuckSeconds) { return false; }

            _current = null;
            return true;
        }

        internal static void Started(Task load)
        {
            _current = load;
            _started = Timing.TotalTime;
        }
    }
}
