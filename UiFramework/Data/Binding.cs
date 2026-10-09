using System.ComponentModel;
using System.Globalization;

namespace UiFramework.Data;

internal sealed class BindingDefinition
{
    internal required object? Source { get; set; }

    internal required string Path { get; init; }

    internal required BindingMode Mode { get; init; }

    internal required Type TargetType { get; init; }

    internal required Action<object?> Apply { get; init; }

    internal Action<object?>? Changed { get; init; }

    internal IValueConverter? Converter { get; init; }

    internal object? ConverterParameter { get; init; }

    internal string? Format { get; init; }

    internal string? Fallback { get; init; }

    internal string? NullValue { get; init; }

    internal required Action<string> Report { get; init; }
}

internal sealed class Binding : IDisposable
{
    private readonly BindingDefinition _definition;
    private readonly BindingPath _path;
    private readonly PropertyChangedEventHandler _handler;
    private bool _echoing;
    private bool _disposed;

    private Binding(BindingDefinition definition, BindingPath path)
    {
        _definition = definition;
        _path = path;
        _handler = OnSourceChanged;
    }

    internal static Binding Attach(BindingDefinition definition)
    {
        BindingPath path = BindingPath.Parse(definition.Path);
        Binding binding = new(definition, path);

        if (definition.Mode != BindingMode.OneWayToSource) { binding.Push(); }

        if (definition.Mode is BindingMode.OneWay or BindingMode.TwoWay)
        {
            path.Hook(definition.Source, binding._handler);
        }

        return binding;
    }

    internal void Retarget(object? source)
    {
        if (_disposed || ReferenceEquals(_definition.Source, source)) { return; }

        if (_definition.Mode is BindingMode.OneWay or BindingMode.TwoWay)
        {
            _path.Unhook(_definition.Source, _handler);
        }

        _definition.Source = source;

        if (_definition.Mode is BindingMode.OneWay or BindingMode.TwoWay)
        {
            _path.Hook(source, _handler);
        }

        if (_definition.Mode != BindingMode.OneWayToSource) { Push(); }
    }

    internal void PushFromTarget(object? value)
    {
        if (_disposed || _echoing) { return; }

        if (_definition.Mode is not (BindingMode.TwoWay or BindingMode.OneWayToSource)) { return; }

        object? converted = _definition.Converter?.ConvertBack(value, _definition.TargetType, _definition.ConverterParameter) ?? value;

        _echoing = true;
        try
        {
            if (!_path.Write(_definition.Source, converted))
            {
                _definition.Report($"'{_definition.Path}' cannot be written");
            }
        }
        finally
        {
            _echoing = false;
        }
    }

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || _echoing) { return; }

        _path.Unhook(_definition.Source, _handler);
        _path.Hook(_definition.Source, _handler);
        Push();
    }

    private void Push()
    {
        object? value;

        try
        {
            value = _path.Read(_definition.Source);
        }
        catch (Exception e)
        {
            _definition.Report($"'{_definition.Path}' could not be read ({e.Message})");
            value = null;
        }

        if (value is null)
        {
            value = _definition.NullValue ?? _definition.Fallback;
        }
        else if (_definition.Converter is { } converter)
        {
            try
            {
                value = converter.Convert(value, _definition.TargetType, _definition.ConverterParameter);
            }
            catch (Exception e)
            {
                _definition.Report($"'{_definition.Path}' could not be converted ({e.Message})");
                value = _definition.Fallback;
            }
        }

        if (value is null) { value = _definition.Fallback; }

        if (_definition.Format is { } format) { value = Format(value, format); }

        Apply(value);
    }

    private void Apply(object? value)
    {
        (object? converted, bool ok) = ValueConversion.Convert(value, _definition.TargetType);
        if (!ok)
        {
            _definition.Report($"'{_definition.Path}' does not give a {_definition.TargetType.Name}");
            return;
        }

        _echoing = true;
        try
        {
            _definition.Apply(converted);
        }
        catch (Exception e)
        {
            _definition.Report($"'{_definition.Path}' could not be applied ({e.Message})");
        }
        finally
        {
            _echoing = false;
        }
    }

    private static object Format(object? value, string format)
        => value switch
        {
            null => format,
            _ => string.Format(CultureInfo.CurrentCulture, format, value)
        };

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _path.Unhook(_definition.Source, _handler);
    }
}
