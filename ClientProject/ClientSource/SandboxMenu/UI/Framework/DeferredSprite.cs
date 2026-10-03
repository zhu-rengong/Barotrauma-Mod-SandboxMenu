using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

// The sprite is handed to the host image only once it is really loaded: GUIImage takes its asynchronous branch just
// until it has loaded once, and a recycled row would otherwise reach its blocking Texture getter on the draw thread.
internal sealed class DeferredSprite : IDisposable
{
    private readonly GUIImage _image;
    private readonly GUICustomComponent _throbber;
    private readonly bool _fit;
    private Sprite? _sprite;
    private Task? _load;

    internal DeferredSprite(RectTransform transform, GUIImage.ScalingMode mode)
    {
        _fit = mode != GUIImage.ScalingMode.None;

        _image = new GUIImage(transform, style: null, scaleToFit: mode)
        {
            CanBeFocused = false
        };

        _throbber = new GUICustomComponent(
            new RectTransform(Vector2.One, transform, Anchor.Center),
            DrawThrobber,
            Load)
        {
            CanBeFocused = false,
            Visible = false
        };

        if (_fit) { _image.RectTransform.SizeChanged += Fit; }
    }

    internal GUIImage Image => _image;

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

        // Anything the image holds while a sprite loads would be drawn, and a lazy one would be loaded by the
        // image itself the moment it is drawn; so it holds nothing and the throbber stands in for it.
        _image.Sprite = null;
        _throbber.Visible = true;
    }

    // Runs while the throbber is up, which is exactly while this icon is unloaded: an icon that has loaded
    // costs nothing after that.
    private void Load(float deltaTime, GUICustomComponent component)
    {
        if (_sprite is not { } sprite) { return; }

        if (_load is { IsCompleted: true })
        {
            _load = null;

            // A load that ended without the sprite (a missing or broken file) leaves nothing to show.
            if (!sprite.Loaded)
            {
                component.Visible = false;
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

    private void Show(Sprite? sprite)
    {
        _load = null;
        _throbber.Visible = false;
        _image.Sprite = sprite;
    }

    private void Fit()
    {
        if (_image.Sprite is not { } sprite) { return; }

        Rectangle source = sprite.SourceRect;
        Rectangle rect = _image.RectTransform.Rect;
        if (source.Width <= 0 || source.Height <= 0 || rect.Width <= 0 || rect.Height <= 0) { return; }

        _image.Scale = Math.Min((float)rect.Width / source.Width, (float)rect.Height / source.Height);
    }

    private static void DrawThrobber(SpriteBatch spriteBatch, GUICustomComponent component)
    {
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
        if (_fit) { _image.RectTransform.SizeChanged -= Fit; }

        _sprite = null;
        _load = null;
        _image.Sprite = null;
    }

    // One decode in flight at a time: the host caches textures by file, so a racing decode of the same file is built
    // only to be dropped, and the GPU memory it takes is not something the garbage collector can see.
    private static class DecodeGate
    {
        private static Task? _current;

        static DecodeGate() => ModLifetime.Unloading += () => _current = null;

        internal static bool Acquire()
        {
            if (_current is { IsCompleted: false }) { return false; }

            _current = null;
            return true;
        }

        internal static void Started(Task load) => _current = load;
    }
}
