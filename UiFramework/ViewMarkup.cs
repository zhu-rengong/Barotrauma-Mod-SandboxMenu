using System.Globalization;

namespace UiFramework;

// The values markup writes in words: rich text and key hints, fonts, colours, anchors and alignments.
public static class ViewMarkup
{
    public static RichString ToRichText(object? value) => value switch
    {
        RichString rich => rich,
        LocalizedString localized => RichString.Rich(localized),
        null => string.Empty,
        _ => RichString.Rich(value.ToString() ?? string.Empty)
    };

    // A key hint behind a label ("Spawn  F1"): the host draws rich text, so the hint is a colour run rather than a
    // second block, and the caller names the template because its colour depends on what it is drawn on. The
    // templates are the mod's own text keys and arrive at startup; without them no hint is drawn.
    public static string? HintKey { get; set; }

    public static string? DarkHintKey { get; set; }

    public static RichString WithShortcut(RichString text, RichString? shortcut, string? hintKey)
    {
        if (shortcut is null || shortcut.Length == 0) { return text; }
        if (hintKey is not { Length: > 0 }) { return text; }

        return RichString.Rich(TextManager.Get(hintKey)
            .Replace("[name]", text.NestedStr, StringComparison.Ordinal)
            .Replace("[key]", shortcut.NestedStr, StringComparison.Ordinal));
    }

    public static bool ToBool(object? value, bool fallback) => value switch
    {
        bool flag => flag,
        string text => bool.TryParse(text, out bool parsed) ? parsed : fallback,
        _ => fallback
    };

    public static float TextScaleOf(string? fontSize, GUIFont font)
        => fontSize is { } raw && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float dip)
            ? UiMetrics.FontScale(dip, font)
            : UiMetrics.TextScale;

    public static GUIFont FontOf(string? name) => name?.ToLowerInvariant() switch
    {
        "subheading" => GUIStyle.SubHeadingFont,
        "large" => GUIStyle.LargeFont,
        "small" => GUIStyle.SmallFont,
        _ => GUIStyle.Font
    };

    public static Anchor AnchorOf(string? name, Anchor fallback)
        => name is not null && Enum.TryParse(name, ignoreCase: true, out Anchor anchor) ? anchor : fallback;

    public static Alignment AlignmentOf(string? name, Alignment fallback)
        => name is not null && Enum.TryParse(name, ignoreCase: true, out Alignment alignment) ? alignment : fallback;

    public static Color ColorOf(string? name, Color fallback) => name?.ToLowerInvariant() switch
    {
        "bright" => UiMetrics.Text,
        "dim" => UiMetrics.TextDim,
        "accent" => UiMetrics.Accent,
        "danger" => UiMetrics.Danger,
        "disabled" => UiMetrics.TextDisabled,
        _ => fallback
    };
}
