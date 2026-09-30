using System.Globalization;
using Microsoft.Xna.Framework;

namespace UiFramework;

internal static class ValueReader
{
    internal static (object? Value, bool Ok) Read(string text, Type type, Func<object?> fallback)
    {
        string trimmed = text.Trim();

        if (type == typeof(string)) { return (text, true); }
        if (type == typeof(RichString)) { return (RichString.Rich(trimmed), true); }
        if (type == typeof(LocalizedString)) { return (TextManager.Get(trimmed), true); }
        if (type == typeof(bool)) { return bool.TryParse(trimmed, out bool flag) ? (flag, true) : (fallback(), false); }
        if (type == typeof(int)) { return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? (number, true) : (fallback(), false); }
        if (type == typeof(float)) { return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? (value, true) : (fallback(), false); }
        if (type == typeof(double)) { return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double big) ? (big, true) : (fallback(), false); }
        if (type.IsEnum) { return Enum.TryParse(type, trimmed, ignoreCase: true, out object? parsed) ? (parsed, true) : (fallback(), false); }

        return (fallback(), false);
    }

    internal static (object? Value, bool Ok) Convert(object? value, Type type)
    {
        if (value is null) { return (null, !type.IsValueType); }
        if (type.IsInstanceOfType(value)) { return (value, true); }
        if (type == typeof(RichString)) { return (ViewMarkup.ToRichText(value), true); }
        if (type == typeof(string)) { return (value.ToString(), true); }

        try
        {
            return (System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture), true);
        }
        catch (Exception)
        {
            return (null, false);
        }
    }
}

internal static class ViewMarkup
{
    internal static RichString ToRichText(object? value) => value switch
    {
        RichString rich => rich,
        LocalizedString localized => RichString.Rich(localized),
        null => string.Empty,
        _ => RichString.Rich(value.ToString() ?? string.Empty)
    };

    // A key hint behind a label ("Spawn  F1"): the host draws rich text, so the hint is a colour run (the template in
    // the texts carries it) rather than a second block. The host's own Replace joins the parts and stays live, so
    // nothing here is a snapshot and a language change refreshes both sides. The caller names the template — the
    // colour of a hint depends on what it is drawn on, never on a default nobody picked.
    internal static RichString WithShortcut(RichString text, RichString? shortcut, string hintKey)
    {
        if (shortcut is null || shortcut.Length == 0) { return text; }

        return RichString.Rich(TextManager.Get(hintKey)
            .Replace("[name]", text.NestedStr, StringComparison.Ordinal)
            .Replace("[key]", shortcut.NestedStr, StringComparison.Ordinal));
    }

    internal static bool ToBool(object? value, bool fallback) => value switch
    {
        bool flag => flag,
        string text => bool.TryParse(text, out bool parsed) ? parsed : fallback,
        _ => fallback
    };

    internal static float TextScaleOf(string? fontSize, GUIFont font)
        => fontSize is { } raw && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float dip)
            ? UiMetrics.FontScale(dip, font)
            : UiMetrics.TextScale;

    internal static GUIFont FontOf(string? name) => name?.ToLowerInvariant() switch
    {
        "subheading" => GUIStyle.SubHeadingFont,
        "large" => GUIStyle.LargeFont,
        "small" => GUIStyle.SmallFont,
        _ => GUIStyle.Font
    };

    internal static Anchor AnchorOf(string? name, Anchor fallback)
        => name is not null && Enum.TryParse(name, ignoreCase: true, out Anchor anchor) ? anchor : fallback;

    internal static Alignment AlignmentOf(string? name, Alignment fallback)
        => name is not null && Enum.TryParse(name, ignoreCase: true, out Alignment alignment) ? alignment : fallback;

    internal static Color ColorOf(string? name, Color fallback) => name?.ToLowerInvariant() switch
    {
        "bright" => UiMetrics.Text,
        "dim" => UiMetrics.TextDim,
        "accent" => UiMetrics.Accent,
        "danger" => UiMetrics.Danger,
        "disabled" => UiMetrics.TextDisabled,
        _ => fallback
    };
}
