namespace UiFramework.Abstractions;

public static class UiMetrics
{
    private static float _rawScale = float.NaN;
    private static float _scale = 1f;
    private static float _textScale = 1f;

    public static float UiScale
    {
        get
        {
            Refresh(GameSettings.CurrentConfig.Graphics.HUDScale);
            return _scale;
        }
    }

    public static float Dip(float value) => value * UiScale;

    public static int DipInt(float value) => (int)MathF.Round(Dip(value));

    public static Point DipSize(float dipWidth, float dipHeight) => new(DipInt(dipWidth), DipInt(dipHeight));

    public static float TextScale
    {
        get
        {
            Refresh(GameSettings.CurrentConfig.Graphics.HUDScale);
            return _textScale;
        }
    }

    public static float FontScale(float dipSize, GUIFont font) => dipSize * TextScale / MathF.Max(1f, font.LineHeight);

    public static Color Text => GUIStyle.TextColorBright;

    public static Color TextDim => GUIStyle.TextColorDim;

    public static Color Accent => GUIStyle.Green;

    public static Color Danger => GUIStyle.Red;

    public static Color TextDisabled => GUIStyle.TextColorDim;

    private static void Refresh(float raw)
    {
        if (raw.Equals(_rawScale)) { return; }

        _rawScale = raw;
        _scale = MathF.Max(0.5f, raw);
        _textScale = GUI.AdjustForTextScale(_scale);
    }
}
