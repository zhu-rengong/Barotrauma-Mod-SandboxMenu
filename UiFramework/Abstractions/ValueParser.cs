using System.Globalization;

namespace UiFramework.Abstractions;

internal static class ValueParser
{
    internal static bool TryFloat(string? text, out float value)
        => float.TryParse(WithoutDip(text), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    internal static bool TryDouble(string? text, out double value)
        => double.TryParse(WithoutDip(text), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    internal static bool TryInt(string? text, out int value)
        => int.TryParse(WithoutDip(text), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    internal static bool TryBool(string? text, bool fallback, out bool value)
    {
        if (text is null)
        {
            value = fallback;
            return false;
        }

        if (bool.TryParse(text, out value)) { return true; }

        value = fallback;
        return false;
    }

    internal static bool TryPercent(string? text, out float ratio)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (!trimmed.EndsWith('%'))
        {
            ratio = 0f;
            return false;
        }

        if (TryFloat(trimmed[..^1], out ratio))
        {
            ratio /= 100f;
            return true;
        }

        return false;
    }

    internal static bool TryDip(string? text, out float dip)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (!trimmed.EndsWith("dip", StringComparison.OrdinalIgnoreCase))
        {
            dip = 0f;
            return false;
        }

        return TryFloat(trimmed[..^3], out dip);
    }

    internal static bool TryColor(string? text, out Color color)
    {
        color = XMLExtensions.ParseColor(text?.Trim() ?? string.Empty, errorMessages: false);
        return text is { Length: > 0 };
    }

    internal static bool TrySeconds(string? text, out float seconds)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            seconds = 0f;
            return false;
        }

        if (!trimmed.Contains(':', StringComparison.Ordinal))
        {
            if (!TryFloat(trimmed, out seconds)) { return false; }

            seconds = MathF.Max(0f, seconds);
            return true;
        }

        float total = 0f;

        foreach (string part in trimmed.Split(':'))
        {
            if (!TryFloat(part, out float value))
            {
                seconds = 0f;
                return false;
            }

            total = total * 60f + value;
        }

        seconds = MathF.Max(0f, total);
        return true;
    }

    private static string WithoutDip(string? text)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        return trimmed.EndsWith("dip", StringComparison.OrdinalIgnoreCase) ? trimmed[..^3].TrimEnd() : trimmed;
    }
}
