using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

// The sprite is handed to the host image only once it is really loaded: GUIImage takes its asynchronous branch just
// until it has loaded once, and a recycled row would otherwise reach its blocking Texture getter on the draw thread.
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

    internal DeferredSprite(RectTransform transform, GUIImage.ScalingMode mode)
    {
        _transform = transform;
        _fit = mode != GUIImage.ScalingMode.None;

        // The icon casts a shadow of itself, the way the game draws one in an inventory slot: the same sprite again, a
        // couple of DIP down and to the right, in black. Both are boxes inside the one the caller hands over — moving
        // that box moves both — and the shadow is built first, so the icon is drawn over it.
        //
        // That box carries a component of its own (the throbber below), because the host walks the rect children of a
        // component and reads the component of each without a null check: a box holding nothing but other boxes breaks
        // every such walk — taking the window out of the update list, a list box clamping its children, and more.
        _shadow = new GUIImage(
            new RectTransform(Vector2.One, transform, Anchor.Center)
            {
                AbsoluteOffset = new Point(UiMetrics.DipInt(Theme.IconShadowOffset), UiMetrics.DipInt(Theme.IconShadowOffset))
            },
            style: null,
            scaleToFit: mode)
        {
            CanBeFocused = false,

            // An image takes its parent's state and draws with the colour of that state, so a shadow the mouse points
            // at — or a shadow whose row is hovered — would be drawn in the icon's own colours instead. Pinning the
            // state to none leaves both images with the one colour they are given.
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
            CanBeFocused = false
        };

        if (_fit) { _transform.SizeChanged += Fit; }
    }

    internal RectTransform Transform => _transform;

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
        _shadow.Sprite = null;
        _waiting = true;
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
                _waiting = false;
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
        _waiting = false;
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

    // This is a component of the box the caller handed over, so it draws in the icon's own place and stands in for the
    // sprite while it is on its way.
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
