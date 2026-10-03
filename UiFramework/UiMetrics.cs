namespace UiFramework;

internal static class UiMetrics
{
    static UiMetrics()
    {
        Reset();

        ModLifetime.Unloading += Reset;
    }

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
        TileSize = 36f;
        SubTextRatio = 0.5f;
        TreeIndentStep = 14f;
        SelectedBarWidth = 3f;
    }

    // The menu is the same size at any resolution: only the player's HUD scale moves it, so the resolution stays out of the layout.
    internal static float UiScale => MathF.Max(0.5f, GameSettings.CurrentConfig.Graphics.HUDScale);

    internal static float Dip(float value) => value * UiScale;

    internal static int DipInt(float value) => (int)MathF.Round(Dip(value));

    internal static Point DipSize(float dipWidth, float dipHeight) => new(DipInt(dipWidth), DipInt(dipHeight));

    internal static float TextScale => GUI.AdjustForTextScale(UiScale);

    internal static float FontScale(float dipSize, GUIFont font) => dipSize * TextScale / MathF.Max(1f, font.LineHeight);

    internal static float RowHeight { get; set; }

    internal static float SectionHeight { get; set; }

    internal static float ControlHeight { get; set; }

    internal static float Pad { get; set; }

    internal static float LabelWidth { get; set; }

    internal static float Gap { get; set; }

    internal static float IconBox { get; set; }

    internal static float IconGap { get; set; }

    internal static float TileSize { get; set; }

    internal static float SubTextRatio { get; set; }

    internal static float TreeIndentStep { get; set; }

    internal static float SelectedBarWidth { get; set; }

    // Read through to the host every time: a GUI colour is a selector over content-defined prefabs, so a value held
    // in a field would keep the colours of content packages that are no longer loaded.
    internal static Color Text => GUIStyle.TextColorBright;

    internal static Color TextDim => GUIStyle.TextColorDim;

    internal static Color Accent => GUIStyle.Green;

    internal static Color Danger => GUIStyle.Red;

    internal static Color TextDisabled => GUIStyle.TextColorDim;
}
