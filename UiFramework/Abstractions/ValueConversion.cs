using System.Collections.Frozen;
using System.Globalization;

namespace UiFramework.Abstractions;

internal static class ValueConversion
{
    private delegate bool TryParser(string text, out object? value);

    private static readonly FrozenDictionary<Type, TryParser> Parsers = new Dictionary<Type, TryParser>
    {
        [typeof(RichString)] = static (string text, out object? value) =>
        {
            value = RichString.Rich(text);
            return true;
        },
        [typeof(LocalizedString)] = static (string text, out object? value) =>
        {
            value = TextManager.Get(text);
            return true;
        },
        [typeof(bool)] = static (string text, out object? value) =>
        {
            bool ok = ValueParser.TryBool(text, false, out bool flag);
            value = flag;
            return ok;
        },
        [typeof(int)] = static (string text, out object? value) =>
        {
            bool ok = ValueParser.TryInt(text, out int number);
            value = number;
            return ok;
        },
        [typeof(float)] = static (string text, out object? value) =>
        {
            bool ok = ValueParser.TryFloat(text, out float number);
            value = number;
            return ok;
        },
        [typeof(double)] = static (string text, out object? value) =>
        {
            bool ok = ValueParser.TryDouble(text, out double number);
            value = number;
            return ok;
        },
        [typeof(Length)] = static (string text, out object? value) =>
        {
            value = Length.Parse(text, Length.Fill);
            return true;
        },
        [typeof(Insets)] = static (string text, out object? value) =>
        {
            value = Insets.Parse(text);
            return true;
        },
        [typeof(Color)] = static (string text, out object? value) =>
        {
            value = ViewMarkup.ColorOf(text, Color.Transparent);
            return true;
        },
        [typeof(Sprite)] = static (string _, out object? value) =>
        {
            value = null;
            return false;
        }
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<(Type Target, Type Source), Func<object, object?>> Conversions =
        new Dictionary<(Type Target, Type Source), Func<object, object?>>
        {
            [(typeof(Length), typeof(string))] = static value => Length.Parse((string)value, Length.Fill),
            [(typeof(Insets), typeof(string))] = static value => Insets.Parse((string)value),
            [(typeof(Color), typeof(string))] = static value => ViewMarkup.ColorOf((string)value, Color.Transparent),
            [(typeof(double), typeof(float))] = static value => (double)(float)value,
            [(typeof(float), typeof(double))] = static value => (float)(double)value
        }.ToFrozenDictionary();

    internal static (object? Value, bool Ok) Parse(string text, Type type, Func<object?> fallback)
    {
        if (type == typeof(string)) { return (text, true); }

        if (Parsers.TryGetValue(type, out TryParser? parse))
        {
            return parse(text.Trim(), out object? value) ? (value, true) : (fallback(), false);
        }

        if (type.IsEnum)
        {
            return Enum.TryParse(type, text.Trim(), ignoreCase: true, out object? parsed) ? (parsed, true) : (fallback(), false);
        }

        return (fallback(), false);
    }

    internal static (object? Value, bool Ok) Convert(object? value, Type type)
    {
        if (value is null) { return (null, !type.IsValueType); }
        if (type.IsInstanceOfType(value)) { return (value, true); }
        if (type == typeof(string)) { return (value.ToString(), true); }
        if (type == typeof(RichString)) { return (ViewMarkup.ToRichText(value), true); }

        if (Conversions.TryGetValue((type, value.GetType()), out Func<object, object?>? convert)) { return (convert(value), true); }

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
