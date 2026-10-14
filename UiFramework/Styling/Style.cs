namespace UiFramework.Styling;

internal sealed class Style : IStyleResource
{
    internal string? Key { get; init; }

    internal string? TargetType { get; init; }

    internal string? BasedOn { get; init; }

    internal IReadOnlyList<Setter> Setters { get; init; } = [];

    internal IReadOnlyList<TriggerBase> Triggers { get; init; } = [];

    internal IReadOnlyList<EventTrigger> Events { get; init; } = [];

    string? IStyleResource.Key => Key;

    internal static Style Read(MarkupNode node, Action<string>? report = null)
    {
        List<Setter> setters = [];
        List<TriggerBase> triggers = [];
        List<EventTrigger> events = [];

        Read(node, report, setters, triggers, events);

        return new Style
        {
            Key = node.Text("Key"),
            TargetType = node.Text("TargetType"),
            BasedOn = node.Text("BasedOn"),
            Setters = setters,
            Triggers = triggers,
            Events = events
        };
    }

    internal static void ReadTriggers(
        MarkupNode node,
        Action<string>? report,
        List<TriggerBase> triggers,
        List<EventTrigger> events)
    {
        foreach (MarkupNode child in node.Content)
        {
            switch (child.Name)
            {
                case "Trigger":
                case "PropertyTrigger":
                case "DataTrigger":
                case "MultiDataTrigger":
                    if (TriggerReader.Read(child, report) is { } trigger) { triggers.Add(trigger); }
                    break;

                case "EventTrigger":
                    if (TriggerReader.ReadEvent(child, report) is { } raised) { events.Add(raised); }
                    break;
            }
        }

        if (node.Child("Triggers") is { } nested) { ReadTriggers(nested, report, triggers, events); }
    }

    internal static List<Setter> ReadSetters(MarkupNode node)
    {
        List<Setter> setters = [];

        foreach (MarkupNode child in node.Content)
        {
            if (!string.Equals(child.Name, "Setter", StringComparison.OrdinalIgnoreCase)) { continue; }
            if (child.Text("Property") is not { } property || child.Value("Value") is not { } value) { continue; }

            setters.Add(new Setter(property, value));
        }

        return setters;
    }

    private static void Read(
        MarkupNode node,
        Action<string>? report,
        List<Setter> setters,
        List<TriggerBase> triggers,
        List<EventTrigger> events)
    {
        setters.AddRange(ReadSetters(node));
        ReadTriggers(node, report, triggers, events);
    }
}
