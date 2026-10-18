using System.Collections.Frozen;

namespace UiFramework;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ElementAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class ElementPropertyAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;

    public ApplyPhase Phase { get; init; } = ApplyPhase.Live;

    public BindingMode Mode { get; init; } = BindingMode.OneWay;

    public bool SettableFromStyle { get; init; } = true;

    public bool KeyText { get; init; }
}

internal sealed record ElementMetadata(
    Func<ElementContext, ViewElement> Create,
    FrozenDictionary<string, PropertyMetadata> Properties);

internal static class AttachedProperties
{
    private static readonly FrozenDictionary<string, PropertyMetadata> _table = Build();

    internal static PropertyMetadata? Find(string name) => _table.GetValueOrDefault(name);

    private static FrozenDictionary<string, PropertyMetadata> Build()
    {
        Dictionary<string, PropertyMetadata> table = new(StringComparer.OrdinalIgnoreCase);

        Add(table, "Grid.Row");
        Add(table, "Grid.Column");
        Add(table, "Grid.RowSpan");
        Add(table, "Grid.ColumnSpan");

        return table.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static void Add(Dictionary<string, PropertyMetadata> table, string name)
        => table[name] = new PropertyMetadata(
            name,
            typeof(int),
            ApplyPhase.Live,
            BindingMode.OneWay,
            SettableFromStyle: true,
            KeyText: false,
            (target, value) => ((ViewElement)target).SetAttached(name, value),
            target => ((ViewElement)target).Attached(name));
}
