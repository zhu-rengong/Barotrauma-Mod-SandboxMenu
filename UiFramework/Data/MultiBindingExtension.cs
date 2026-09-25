namespace UiFramework.Data;

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
