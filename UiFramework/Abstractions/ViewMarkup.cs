namespace UiFramework.Abstractions;

public static class ViewMarkup
{
    public static RichString ToRichText(object? value) => value switch
    {
        RichString rich => rich,
        LocalizedString localized => RichString.Rich(localized),
        null => string.Empty,
        _ => RichString.Rich(value.ToString() ?? string.Empty)
    };

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
        string text => ValueParser.TryBool(text, fallback, out bool parsed) ? parsed : fallback,
        _ => fallback
    };

    public static float TextScaleOf(string? fontSize, GUIFont font)
        => fontSize is { } raw && ValueParser.TryFloat(raw, out float dip)
            ? UiMetrics.FontScale(dip, font)
            : UiMetrics.TextScale;

    public static GUIFont FontOf(string? name)
    {
        if (name is not null)
        {
            if (name.Equals("subheading", StringComparison.OrdinalIgnoreCase)) { return GUIStyle.SubHeadingFont; }
            if (name.Equals("large", StringComparison.OrdinalIgnoreCase)) { return GUIStyle.LargeFont; }
            if (name.Equals("small", StringComparison.OrdinalIgnoreCase)) { return GUIStyle.SmallFont; }
        }

        return GUIStyle.Font;
    }

    public static Anchor AnchorOf(string? name, Anchor fallback)
        => name is not null && Enum.TryParse(name, ignoreCase: true, out Anchor anchor) ? anchor : fallback;

    public static Alignment AlignmentOf(string? name, Alignment fallback)
        => name is not null && Enum.TryParse(name, ignoreCase: true, out Alignment alignment) ? alignment : fallback;

    public static Color ColorOf(string? name, Color fallback)
    {
        if (name is not { Length: > 0 }) { return fallback; }

        string text = name.Trim();

        Color? themed = null;

        if (text.Equals("bright", StringComparison.OrdinalIgnoreCase)) { themed = UiMetrics.Text; }
        else if (text.Equals("dim", StringComparison.OrdinalIgnoreCase)) { themed = UiMetrics.TextDim; }
        else if (text.Equals("accent", StringComparison.OrdinalIgnoreCase)) { themed = UiMetrics.Accent; }
        else if (text.Equals("danger", StringComparison.OrdinalIgnoreCase)) { themed = UiMetrics.Danger; }
        else if (text.Equals("warning", StringComparison.OrdinalIgnoreCase)) { themed = UiMetrics.Warning; }
        else if (text.Equals("disabled", StringComparison.OrdinalIgnoreCase)) { themed = UiMetrics.TextDisabled; }

        if (themed is { } color) { return color; }

        return ValueParser.TryColor(text, out Color parsed) ? parsed : fallback;
    }

    public static float SecondsOf(string? text, float fallback, Action<string>? report = null)
    {
        if (text is not { Length: > 0 }) { return fallback; }

        if (ValueParser.TrySeconds(text, out float seconds)) { return seconds; }

        report?.Invoke($"'{text}' is not a duration (seconds, or h:mm:ss)");
        return fallback;
    }
}
