using System.Collections.Frozen;

namespace UiFramework;

internal enum ApplyPhase
{
    Construct,
    Live
}

[AttributeUsage(AttributeTargets.Class)]
internal sealed class ElementAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class ElementPropertyAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;

    public ApplyPhase Phase { get; init; } = ApplyPhase.Live;

    public BindingMode Mode { get; init; } = BindingMode.OneWay;

    public bool SettableFromStyle { get; init; } = true;

    public bool KeyText { get; init; }
}

internal sealed record PropertyMetadata(
    string Name,
    Type ValueType,
    ApplyPhase Phase,
    BindingMode DefaultMode,
    bool SettableFromStyle,
    bool KeyText,
    Action<object, object?> Apply);

internal sealed record ElementMetadata(
    string Name,
    Func<ElementContext, ViewElement> Create,
    FrozenDictionary<string, PropertyMetadata> Properties);
