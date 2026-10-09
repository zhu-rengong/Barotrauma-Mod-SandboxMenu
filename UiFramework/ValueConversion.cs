using System.Globalization;

namespace UiFramework;

internal static class ValueConversion
{
    internal static (object? Value, bool Ok) Parse(string text, Type type, Func<object?> fallback)
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
