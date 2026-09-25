namespace UiFramework.Styling;

internal sealed record Setter(string Property, MarkupValue Value);

internal sealed class Style
{
    internal string? Key { get; init; }

    internal string? TargetType { get; init; }

    internal string? BasedOn { get; init; }

    internal IReadOnlyList<Setter> Setters { get; init; } = [];

    internal IReadOnlyList<DataTrigger> Triggers { get; init; } = [];

    internal static Style Read(MarkupNode node)
    {
        List<Setter> setters = [];
        List<DataTrigger> triggers = [];

        foreach (MarkupNode child in node.Content)
        {
            if (string.Equals(child.Name, "Setter", StringComparison.OrdinalIgnoreCase))
            {
                if (child.Text("Property") is { } property && child.Value("Value") is { } value)
                {
                    setters.Add(new Setter(property, value));
                }
            }
            else if (string.Equals(child.Name, "DataTrigger", StringComparison.OrdinalIgnoreCase))
            {
                if (child.Text("Binding") is { } path && child.Value("Value") is { } expected)
                {
                    List<Setter> triggerSetters = [];

                    foreach (MarkupNode triggerChild in child.Content)
                    {
                        if (string.Equals(triggerChild.Name, "Setter", StringComparison.OrdinalIgnoreCase)
                            && triggerChild.Text("Property") is { } triggerProperty
                            && triggerChild.Value("Value") is { } triggerValue)
                        {
                            triggerSetters.Add(new Setter(triggerProperty, triggerValue));
                        }
                    }

                    triggers.Add(new DataTrigger(path, expected, triggerSetters));
                }
            }
        }

        return new Style
        {
            Key = node.Text("Key"),
            TargetType = node.Text("TargetType"),
            BasedOn = node.Text("BasedOn"),
            Setters = setters,
            Triggers = triggers
        };
    }
}

internal sealed class DataTrigger(string path, MarkupValue value, IReadOnlyList<Setter> setters)
{
    internal string Path { get; } = path;

    internal MarkupValue Value { get; } = value;

    internal IReadOnlyList<Setter> Setters { get; } = setters;
}
