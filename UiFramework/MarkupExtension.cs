namespace UiFramework;

internal abstract class MarkupExtension
{
    private static Dictionary<string, Func<string, MarkupExtension>>? _kinds;
    private static Dictionary<string, Func<MarkupNode, MarkupExtension>>? _elements;

    static MarkupExtension() => StaticState.Register(() => (_kinds, _elements) = (null, null));

    private static Dictionary<string, Func<string, MarkupExtension>> Kinds => _kinds ??= new(StringComparer.OrdinalIgnoreCase)
    {
        ["Binding"] = static body => new BindingExtension(BindingOptions.Parse(body)),
        ["StaticResource"] = StaticResourceExtension.Read,
        ["DynamicResource"] = DynamicResourceExtension.Read
    };

    private static Dictionary<string, Func<MarkupNode, MarkupExtension>> Elements => _elements ??= new(StringComparer.OrdinalIgnoreCase)
    {
        ["Binding"] = BindingExtension.ReadNode,
        ["MultiBinding"] = MultiBindingExtension.Read
    };

    internal static MarkupExtension? ForElement(MarkupNode node)
        => Elements.TryGetValue(node.Name, out Func<MarkupNode, MarkupExtension>? read) ? read(node) : null;

    internal abstract void Apply(PropertyAssignment assignment);

    internal static MarkupExtension Parse(string text)
    {
        string body = text[1..^1].Trim();
        string name = body.Split([',', ' '], 2, StringSplitOptions.TrimEntries)[0];

        return Kinds.TryGetValue(name, out Func<string, MarkupExtension>? parse) ? parse(body) : new UnknownExtension(body);
    }
}

internal sealed class UnknownExtension(string body) : MarkupExtension
{
    private readonly string _body = body;

    internal override void Apply(PropertyAssignment assignment)
        => assignment.Report($"unknown markup extension '{{{_body}}}'");
}
