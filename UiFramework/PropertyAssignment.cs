using System.Windows.Input;
using UiFramework.Styling;

namespace UiFramework;

internal sealed class PropertyAssignment(
    ViewContext view,
    MarkupNode node,
    ViewElement element,
    PropertyMetadata property,
    object? target = null)
{
    internal PropertyMetadata Property { get; } = property;

    internal ViewElement Element { get; } = element;

    private object Target { get; } = target ?? element;

    internal void SetText(string text)
    {
        if (Property.KeyText && !string.IsNullOrEmpty(text))
        {
            SetKeyText(text);
            return;
        }

        (object? value, bool ok) = ValueConversion.Parse(text, Property.ValueType, () => null);
        if (!ok)
        {
            Report($"'{text}' is not a {Friendly(Property.ValueType)}");
            return;
        }

        Property.Apply(Target, value);
    }

    internal void SetValue(object? value, string source)
    {
        (object? converted, bool ok) = ValueConversion.Convert(value, Property.ValueType);
        if (!ok)
        {
            Report($"'{source}' does not give a {Friendly(Property.ValueType)}");
            return;
        }

        Property.Apply(Target, converted);
    }

    internal void Bind(BindingOptions options)
    {
        object? source = ResolveSource(options, out string sourceName);
        Binding? binding = null;

        BindingMode mode = options.Mode ?? Property.DefaultMode;

        if (mode is BindingMode.TwoWay or BindingMode.OneWayToSource && Element is not IPropertyObserver)
        {
            Report($"'{Property.Name}' cannot be written back to '{sourceName}'");
            return;
        }

        BindingDefinition definition = new()
        {
            Source = source,
            Path = options.Path,
            Mode = mode,
            TargetType = Property.ValueType,
            Apply = value => SetValue(value, sourceName),
            Changed = value => view.Dispatch(() => binding?.PushFromTarget(value)),
            Converter = options.Converter is { } name ? ValueConverters.Find(name) : null,
            ConverterParameter = options.ConverterParameter,
            Format = options.Format,
            Fallback = options.Fallback,
            NullValue = options.NullValue,
            Report = Report
        };

        binding = Binding.Attach(definition);
        view.Own(binding);

        if (mode is BindingMode.TwoWay or BindingMode.OneWayToSource && Element is IPropertyObserver observer)
        {
            observer.Observe(Property.Name, definition.Changed);
        }

        if (Property.ValueType == typeof(ICommand) && Element is ICommandElement commandElement)
        {
            view.Own(Binding.Attach(new BindingDefinition
            {
                Source = source,
                Path = string.Empty,
                Mode = BindingMode.OneWay,
                TargetType = typeof(object),
                Apply = _ => commandElement.RefreshCanExecute(),
                Report = Report
            }));
        }
    }

    internal void BindMulti(IReadOnlyList<BindingOptions> bindings, string? converter, string? parameter)
    {
        if (converter is not { } name || ValueConverters.FindMulti(name) is not { } multi)
        {
            Report($"unknown multi value converter '{converter}'");
            return;
        }

        object?[] values = new object?[bindings.Count];

        void Recompute() => SetValue(multi.Convert(values, Property.ValueType, parameter), "multi binding");

        for (int i = 0; i < bindings.Count; i++)
        {
            int index = i;
            BindingOptions options = bindings[i];
            object? source = ResolveSource(options, out string sourceName);

            view.Own(Binding.Attach(new BindingDefinition
            {
                Source = source,
                Path = options.Path,
                Mode = BindingMode.OneWay,
                TargetType = typeof(object),
                Apply = value =>
                {
                    values[index] = value;
                    Recompute();
                },
                Report = Report
            }));
        }
    }

    internal void SetResource(string key, bool reevaluate)
    {
        if (view.FindResource(key, Element) is not { } resource)
        {
            Report($"no resource named '{key}'");
            return;
        }

        if (resource is Style)
        {
            Report($"'{key}' is a style and cannot be the value of '{Property.Name}'");
            return;
        }

        if (reevaluate && resource is string text)
        {
            SetKeyText(text);
            return;
        }

        SetValue(resource, $"resource {key}");
    }

    internal void Report(string message) => view.Diagnostics.Report($"{Property.Name}: {message}", node);

    private object? ResolveSource(BindingOptions options, out string sourceName)
    {
        if (options.ElementName is { } name)
        {
            if (view.Names.Find(name) is not { } element)
            {
                Report($"no element named '{name}'");
                sourceName = name;
                return null;
            }

            sourceName = $"{name}.{options.Path}";
            return element.DataContext;
        }

        sourceName = options.Path;
        return Element.DataContext;
    }

    private void SetKeyText(string key)
    {
        (object? value, bool ok) = ValueConversion.Convert(TextManager.Get(key), Property.ValueType);
        if (ok) { Property.Apply(Target, value); }
    }

    private static string Friendly(Type type) => type.Name;
}
