using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SandboxMenu.UI;

internal static class SpawnPointPicker
{
    private static Action<Vector2>? _onPicked;
    private static Action? _onCancelled;

    private static LocalizedString _hint = LocalizedString.EmptyString;
    private static float _hintScale;
    private static Vector2 _hintSize;
    private static float _offsetX;
    private static float _offsetY;
    private static int _arm;
    private static int _thickness;
    private static int _pad;
    private static int _padY;

    public static bool IsActive { get; private set; }

    public static void Begin(Action<Vector2> onPicked, Action? onCancelled = null)
    {
        _onPicked = onPicked;
        _onCancelled = onCancelled;
        IsActive = true;

        _hint = TextManager.Get("sandboxmenu.picker.hint");
        _hintScale = UiMetrics.FontScale(Theme.PickerHintFontSize, GUIStyle.SmallFont);
        _hintSize = GUIStyle.SmallFont.MeasureString(_hint, false) * _hintScale;
        _offsetX = UiMetrics.Dip(Theme.PickerHintOffsetX);
        _offsetY = UiMetrics.Dip(Theme.PickerHintOffsetY);
        _arm = UiMetrics.DipInt(Theme.PickerHintArmLength);
        _thickness = UiMetrics.DipInt(Theme.LineThickness);
        _pad = UiMetrics.DipInt(Theme.Pad);
        _padY = UiMetrics.DipInt(Theme.PickerHintPaddingY);
    }

    public static void Cancel()
    {
        if (!IsActive) { return; }

        Action? cancelled = _onCancelled;
        Release();
        cancelled?.Invoke();
    }

    internal static void Release()
    {
        IsActive = false;
        _onPicked = null;
        _onCancelled = null;
        _hint = LocalizedString.EmptyString;
        _hintSize = Vector2.Zero;
    }

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
        Release();
        picked?.Invoke(worldPosition);
        return true;
    }

    public static void DrawHint(SpriteBatch spriteBatch)
    {
        if (!IsActive) { return; }

        LocalizedString hint = _hint;
        float textScale = _hintScale;
        Vector2 textSize = _hintSize;
        Vector2 mouse = PlayerInput.MousePosition;
        Vector2 textPosition = new(mouse.X + _offsetX, mouse.Y + _offsetY);

        int cx = (int)mouse.X;
        int cy = (int)mouse.Y;
        Theme.Fill(spriteBatch, new Rectangle(cx - _arm, cy, _arm * 2 + 1, _thickness), Theme.Accent);
        Theme.Fill(spriteBatch, new Rectangle(cx, cy - _arm, _thickness, _arm * 2 + 1), Theme.Accent);

        Rectangle frame = new(
            (int)textPosition.X - _pad,
            (int)textPosition.Y - _padY,
            (int)textSize.X + _pad * 2,
            (int)textSize.Y + _padY * 2);

        Theme.Fill(spriteBatch, frame, Theme.OverlayFill);
        Theme.Outline(spriteBatch, frame, Theme.OverlayLine);

        GUIStyle.SmallFont.DrawString(spriteBatch, hint, textPosition, Theme.Text, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
    }

}
