using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SandboxMenu.UI;

public static class SpawnLocationPicker
{
    private static Action<Vector2>? _onPicked;
    private static Action? _onCancelled;

    static SpawnLocationPicker() => StaticState.Register(Abort);

    public static bool IsActive { get; private set; }

    public static void Begin(Action<Vector2> onPicked, Action? onCancelled = null)
    {
        _onPicked = onPicked;
        _onCancelled = onCancelled;
        IsActive = true;
    }

    public static void Cancel()
    {
        if (!IsActive) { return; }

        Action? cancelled = _onCancelled;
        Reset();
        cancelled?.Invoke();
    }

    internal static void Abort() => Reset();

    public static bool Update()
    {
        if (!IsActive || Screen.Selected != GameMain.GameScreen) { return false; }

        if (PlayerInput.KeyHit(Keys.Escape) || PlayerInput.SecondaryMouseButtonClicked())
        {
            Cancel();
            return true;
        }

        if (!PlayerInput.PrimaryMouseButtonClicked() || GUI.MouseOn is not null) { return false; }

        Vector2 worldPosition = GameMain.GameScreen.Cam.ScreenToWorld(PlayerInput.MousePosition);
        Action<Vector2>? picked = _onPicked;
        Reset();
        picked?.Invoke(worldPosition);
        return true;
    }

    public static void DrawHint(SpriteBatch spriteBatch)
    {
        if (!IsActive) { return; }

        LocalizedString hint = TextManager.Get("sandboxmenu.picker.hint");
        float textScale = MenuTheme.FontScale(MenuTheme.PickerHintFontSize, GUIStyle.SmallFont);
        Vector2 textSize = GUIStyle.SmallFont.MeasureString(hint, false) * textScale;
        Vector2 mouse = PlayerInput.MousePosition;
        var textPosition = new Vector2(
            mouse.X + MenuTheme.Dip(MenuTheme.PickerHintOffsetX),
            mouse.Y + MenuTheme.Dip(MenuTheme.PickerHintOffsetY));

        int cx = (int)mouse.X;
        int cy = (int)mouse.Y;
        int arm = MenuTheme.DipInt(8f);
        int thickness = MenuTheme.DipInt(MenuTheme.LineThickness);
        MenuPaint.Fill(spriteBatch, new Rectangle(cx - arm, cy, arm * 2 + 1, thickness), MenuTheme.Accent);
        MenuPaint.Fill(spriteBatch, new Rectangle(cx, cy - arm, thickness, arm * 2 + 1), MenuTheme.Accent);

        int pad = MenuTheme.DipInt(MenuTheme.Pad);
        int padY = MenuTheme.DipInt(MenuTheme.PickerHintPaddingY);
        var frame = new Rectangle(
            (int)textPosition.X - pad,
            (int)textPosition.Y - padY,
            (int)textSize.X + pad * 2,
            (int)textSize.Y + padY * 2);

        MenuPaint.Fill(spriteBatch, frame, MenuTheme.OverlayFill);
        MenuPaint.Outline(spriteBatch, frame, MenuTheme.OverlayLine);

        GUIStyle.SmallFont.DrawString(spriteBatch, hint, textPosition, MenuTheme.Text, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
    }

    private static void Reset()
    {
        IsActive = false;
        _onPicked = null;
        _onCancelled = null;
    }
}
