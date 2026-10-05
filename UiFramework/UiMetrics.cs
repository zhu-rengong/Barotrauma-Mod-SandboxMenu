namespace UiFramework;

// Resolution independence, and nothing else: sizes arrive in DIP and leave as pixels through UiScale, text is sized
// through the host's text scale. The menu's own numbers are the mod's and arrive as tokens (UiHost.RegisterTokens).
public static class UiMetrics
{
    // The menu is the same size at any resolution: only the player's HUD scale moves it, so the resolution stays out of the layout.
    public static float UiScale => MathF.Max(0.5f, GameSettings.CurrentConfig.Graphics.HUDScale);

    public static float Dip(float value) => value * UiScale;

    public static int DipInt(float value) => (int)MathF.Round(Dip(value));

    public static Point DipSize(float dipWidth, float dipHeight) => new(DipInt(dipWidth), DipInt(dipHeight));

    public static float TextScale => GUI.AdjustForTextScale(UiScale);

    public static float FontScale(float dipSize, GUIFont font) => dipSize * TextScale / MathF.Max(1f, font.LineHeight);

    // Read through to the host every time: a GUI colour is a selector over content-defined prefabs, so a value held
    // in a field would keep the colours of content packages that are no longer loaded.
    public static Color Text => GUIStyle.TextColorBright;

    public static Color TextDim => GUIStyle.TextColorDim;

    public static Color Accent => GUIStyle.Green;

    public static Color Danger => GUIStyle.Red;

    public static Color TextDisabled => GUIStyle.TextColorDim;
}
