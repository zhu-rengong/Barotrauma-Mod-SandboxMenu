using System.ComponentModel;
using System.Globalization;

namespace UiFramework.Data;

internal sealed class BindingRequest
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
    private readonly BindingRequest _request;
    private readonly BindingPath _path;
    private readonly PropertyChangedEventHandler _handler;
    private bool _echoing;
    private bool _disposed;

    private Binding(BindingRequest request, BindingPath path)
    {
        _request = request;
        _path = path;
        _handler = OnSourceChanged;
    }

    internal static Binding Attach(BindingRequest request)
    {
        BindingPath path = BindingPath.Parse(request.Path);
        Binding binding = new(request, path);

        if (request.Mode != BindingMode.OneWayToSource) { binding.Push(); }

        if (request.Mode is BindingMode.OneWay or BindingMode.TwoWay)
        {
            path.Hook(request.Source, binding._handler);
        }

        return binding;
    }

    internal void Retarget(object? source)
    {
        if (_disposed || ReferenceEquals(_request.Source, source)) { return; }

        if (_request.Mode is BindingMode.OneWay or BindingMode.TwoWay)
        {
            _path.Unhook(_request.Source, _handler);
        }

        _request.Source = source;

        if (_request.Mode is BindingMode.OneWay or BindingMode.TwoWay)
        {
            _path.Hook(source, _handler);
        }

        if (_request.Mode != BindingMode.OneWayToSource) { Push(); }
    }

    internal void PushFromTarget(object? value)
    {
        if (_disposed || _echoing) { return; }

        if (_request.Mode is not (BindingMode.TwoWay or BindingMode.OneWayToSource)) { return; }

        object? converted = _request.Converter?.ConvertBack(value, _request.TargetType, _request.ConverterParameter) ?? value;

        _echoing = true;
        try
        {
            if (!_path.Write(_request.Source, converted))
            {
                _request.Report($"'{_request.Path}' cannot be written");
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

        _path.Unhook(_request.Source, _handler);
        _path.Hook(_request.Source, _handler);
        Push();
    }

    private void Push()
    {
        object? value;

        try
        {
            value = _path.Read(_request.Source);
        }
        catch (Exception e)
        {
            _request.Report($"'{_request.Path}' could not be read ({e.Message})");
            value = null;
        }

        if (value is null)
        {
            value = _request.NullValue ?? _request.Fallback;
        }
        else if (_request.Converter is { } converter)
        {
            try
            {
                value = converter.Convert(value, _request.TargetType, _request.ConverterParameter);
            }
            catch (Exception e)
            {
                _request.Report($"'{_request.Path}' could not be converted ({e.Message})");
                value = _request.Fallback;
            }
        }

        if (value is null) { value = _request.Fallback; }

        if (_request.Format is { } format) { value = Format(value, format); }

        Apply(value);
    }

    private void Apply(object? value)
    {
        (object? converted, bool ok) = ValueReader.Convert(value, _request.TargetType);
        if (!ok)
        {
            _request.Report($"'{_request.Path}' does not give a {_request.TargetType.Name}");
            return;
        }

        _echoing = true;
        try
        {
            _request.Apply(converted);
        }
        catch (Exception e)
        {
            _request.Report($"'{_request.Path}' could not be applied ({e.Message})");
        }
        finally
        {
            _echoing = false;
        }
    }

    private object Format(object? value, string format)
    {
        string pattern = format;

        return value switch
        {
            null => pattern,
            _ => string.Format(CultureInfo.CurrentCulture, pattern, value)
        };
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _path.Unhook(_request.Source, _handler);
    }
}
