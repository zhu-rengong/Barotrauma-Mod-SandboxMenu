namespace UiFramework.Abstractions;

internal abstract class MarkupExtension
{
    private static Dictionary<string, Func<string, MarkupExtension>>? _kinds;
    private static Dictionary<string, Func<MarkupNode, MarkupExtension>>? _elements;

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

internal sealed class BindingExtension(BindingOptions options) : MarkupExtension
{
    private readonly BindingOptions _options = options;

    internal static MarkupExtension Read(string body) => new BindingExtension(BindingOptions.Parse(body));

    internal static MarkupExtension ReadNode(MarkupNode node) => new BindingExtension(BindingOptions.Read(node));

    internal override void Apply(PropertyAssignment assignment) => assignment.Bind(_options);
}

internal sealed class MultiBindingExtension : MarkupExtension
{
    private readonly IReadOnlyList<BindingOptions> _bindings;
    private readonly string? _converter;
    private readonly string? _parameter;

    private MultiBindingExtension(IReadOnlyList<BindingOptions> bindings, string? converter, string? parameter)
    {
        _bindings = bindings;
        _converter = converter;
        _parameter = parameter;
    }

    internal static MarkupExtension Read(MarkupNode node)
    {
        List<BindingOptions> bindings = [];

        foreach (MarkupNode child in node.Content)
        {
            if (string.Equals(child.Name, "Binding", StringComparison.OrdinalIgnoreCase))
            {
                bindings.Add(BindingOptions.Read(child));
            }
        }

        return new MultiBindingExtension(bindings, node.Text("Converter"), node.Text("ConverterParameter"));
    }

    internal override void Apply(PropertyAssignment assignment) => assignment.BindMulti(_bindings, _converter, _parameter);
}

internal sealed class StaticResourceExtension(string key) : MarkupExtension
{
    private readonly string _key = key;

    internal static MarkupExtension Read(string body) => new StaticResourceExtension(body.Split(' ', 2, StringSplitOptions.TrimEntries)[^1]);

    internal override void Apply(PropertyAssignment assignment) => assignment.SetResource(_key, reevaluate: false);
}

internal sealed class DynamicResourceExtension(string key) : MarkupExtension
{
    private readonly string _key = key;

    internal static MarkupExtension Read(string body) => new DynamicResourceExtension(body.Split(' ', 2, StringSplitOptions.TrimEntries)[^1]);

    internal override void Apply(PropertyAssignment assignment) => assignment.SetResource(_key, reevaluate: true);
}
