using System.Globalization;

namespace UiFramework.Data;

public interface IValueConverter
{
    object? Convert(object? value, Type targetType, object? parameter);

    object? ConvertBack(object? value, Type targetType, object? parameter);
}

public interface IMultiValueConverter
{
    object? Convert(IReadOnlyList<object?> values, Type targetType, object? parameter);
}

internal static class ValueConverters
{
    private static readonly Dictionary<string, IValueConverter> _single = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Inverse"] = new InverseBoolConverter(),
        ["Percent"] = new PercentConverter()
    };

    private static readonly Dictionary<string, IMultiValueConverter> _multi = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Join"] = new JoinConverter()
    };

    internal static void Register(string name, IValueConverter converter) => _single[name] = converter;

    internal static void RegisterMulti(string name, IMultiValueConverter converter) => _multi[name] = converter;

    internal static IValueConverter? Find(string name) => _single.GetValueOrDefault(name);

    internal static IMultiValueConverter? FindMulti(string name) => _multi.GetValueOrDefault(name);
}

internal sealed class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter) => value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter) => value is not true;
}

internal sealed class PercentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter)
        => value is null ? null : ToNumber(value) * 100.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter)
        => value is null ? null : ToNumber(value) / 100.0;

    private static double ToNumber(object value)
        => value is IConvertible convertible ? convertible.ToDouble(CultureInfo.InvariantCulture) : 0.0;
}

internal sealed class JoinConverter : IMultiValueConverter
{
    public object? Convert(IReadOnlyList<object?> values, Type targetType, object? parameter)
        => string.Join(
            parameter as string ?? ", ",
            values.Where(value => value is { } item && !string.IsNullOrEmpty(item.ToString())));
}
