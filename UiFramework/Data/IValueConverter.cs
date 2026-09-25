namespace UiFramework.Data;

internal interface IValueConverter
{
    object? Convert(object? value, Type targetType, object? parameter);

    object? ConvertBack(object? value, Type targetType, object? parameter);
}

internal interface IMultiValueConverter
{
    object? Convert(IReadOnlyList<object?> values, Type targetType, object? parameter);
}

internal static class ValueConverters
{
    private static readonly Dictionary<string, IValueConverter> Single = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Inverse"] = new InverseBoolConverter()
    };

    private static readonly Dictionary<string, IMultiValueConverter> Multi = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Join"] = new JoinConverter()
    };

    static ValueConverters() => StaticState.Register(Clear);

    internal static void Clear()
    {
        Single.Clear();
        Multi.Clear();
    }

    internal static IValueConverter? Find(string name) => Single.GetValueOrDefault(name);

    internal static IMultiValueConverter? FindMulti(string name) => Multi.GetValueOrDefault(name);
}

internal sealed class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter) => value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter) => value is not true;
}

internal sealed class JoinConverter : IMultiValueConverter
{
    public object? Convert(IReadOnlyList<object?> values, Type targetType, object? parameter)
        => string.Join(
            parameter as string ?? ", ",
            values.Where(value => value is { } text && !string.IsNullOrEmpty(text.ToString())));
}
