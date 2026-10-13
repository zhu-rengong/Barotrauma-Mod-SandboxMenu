using System.Globalization;

namespace UiFramework.Abstractions;

internal static class ValueConversion
{
    internal static (object? Value, bool Ok) Parse(string text, Type type, Func<object?> fallback)
    {
        string trimmed = text.Trim();

        if (type == typeof(string)) { return (text, true); }
        if (type == typeof(RichString)) { return (RichString.Rich(trimmed), true); }
        if (type == typeof(LocalizedString)) { return (TextManager.Get(trimmed), true); }
        if (type == typeof(bool)) { return ValueParser.TryBool(trimmed, false, out bool flag) ? (flag, true) : (fallback(), false); }
        if (type == typeof(int)) { return ValueParser.TryInt(trimmed, out int number) ? (number, true) : (fallback(), false); }
        if (type == typeof(float)) { return ValueParser.TryFloat(trimmed, out float value) ? (value, true) : (fallback(), false); }
        if (type == typeof(double)) { return ValueParser.TryDouble(trimmed, out double big) ? (big, true) : (fallback(), false); }
        if (type == typeof(Length)) { return (Length.Parse(trimmed, Length.Fill), true); }
        if (type == typeof(Insets)) { return (Insets.Parse(trimmed), true); }
        if (type == typeof(Color)) { return (ViewMarkup.ColorOf(trimmed, Color.Transparent), true); }
        if (type == typeof(Sprite)) { return (null, false); }
        if (type.IsEnum) { return Enum.TryParse(type, trimmed, ignoreCase: true, out object? parsed) ? (parsed, true) : (fallback(), false); }

        return (fallback(), false);
    }

    internal static (object? Value, bool Ok) Convert(object? value, Type type)
    {
        if (value is null) { return (null, !type.IsValueType); }
        if (type.IsInstanceOfType(value)) { return (value, true); }
        if (type == typeof(RichString)) { return (ViewMarkup.ToRichText(value), true); }
        if (type == typeof(string)) { return (value.ToString(), true); }
        if (type == typeof(Length) && value is string lengthText) { return (Length.Parse(lengthText, Length.Fill), true); }
        if (type == typeof(Insets) && value is string insetsText) { return (Insets.Parse(insetsText), true); }
        if (type == typeof(Color) && value is string colorText) { return (ViewMarkup.ColorOf(colorText, Color.Transparent), true); }
        if (type == typeof(double) && value is float single) { return ((double)single, true); }
        if (type == typeof(float) && value is double big) { return ((float)big, true); }

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
