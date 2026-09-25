namespace UiFramework;

internal static class UiMetrics
{
    static UiMetrics() => StaticState.Register(Reset);

    internal static void Reset()
    {
        RowHeight = 0.075f;
        SectionHeight = 0.085f;
        ControlHeight = 0.84f;
        Pad = 6f;
        LabelWidth = 0.38f;
        Gap = 4f;
        IconBox = 24f;
        IconGap = 6f;
        SubTextRatio = 0.5f;
        TreeIndentStep = 14f;
        SelectedBarWidth = 3f;
    }

    public static float UiScale => MathF.Max(0.5f, GameSettings.CurrentConfig.Graphics.HUDScale);

    public static float Dip(float value) => value * UiScale;

    public static int DipInt(float value) => (int)Math.Round(Dip(value));

    public static float TextScale => UiScale;

    public static float FontScale(float dipSize, GUIFont font) => dipSize * UiScale / MathF.Max(1f, font.LineHeight);

    public static float RowHeight { get; set; } = 0.075f;

    public static float SectionHeight { get; set; } = 0.085f;

    public static float ControlHeight { get; set; } = 0.84f;

    public static float Pad { get; set; } = 6f;

    public static float LabelWidth { get; set; } = 0.38f;

    public static float Gap { get; set; } = 4f;

    public static float IconBox { get; set; } = 24f;

    public static float IconGap { get; set; } = 6f;

    public static float SubTextRatio { get; set; } = 0.5f;

    public static float TreeIndentStep { get; set; } = 14f;

    public static float SelectedBarWidth { get; set; } = 3f;

    public static Color Text { get; } = GUIStyle.TextColorBright;

    public static Color TextDim { get; } = GUIStyle.TextColorDim;

    public static Color Accent { get; } = GUIStyle.Green;

    public static Color Danger { get; } = GUIStyle.Red;

    public static Color TextDisabled { get; } = GUIStyle.TextColorDim;
}
