namespace UiFramework;

public static class UiMetrics
{
    public static float UiScale => MathF.Max(0.5f, GameSettings.CurrentConfig.Graphics.HUDScale);

    public static float Dip(float value) => value * UiScale;

    public static int DipInt(float value) => (int)MathF.Round(Dip(value));

    public static Point DipSize(float dipWidth, float dipHeight) => new(DipInt(dipWidth), DipInt(dipHeight));

    public static float TextScale => GUI.AdjustForTextScale(UiScale);

    public static float FontScale(float dipSize, GUIFont font) => dipSize * TextScale / MathF.Max(1f, font.LineHeight);

    public static Color Text => GUIStyle.TextColorBright;

    public static Color TextDim => GUIStyle.TextColorDim;

    public static Color Accent => GUIStyle.Green;

    public static Color Danger => GUIStyle.Red;

    public static Color TextDisabled => GUIStyle.TextColorDim;
}
