namespace UiFramework;

internal sealed class BindingExtension(BindingOptions options) : MarkupExtension
{
    private readonly BindingOptions _options = options;

    internal static MarkupExtension Read(string body) => new BindingExtension(BindingOptions.Parse(body));

    internal static MarkupExtension ReadNode(MarkupNode node) => new BindingExtension(BindingOptions.Read(node));

    internal override void Apply(PropertyAssignment assignment) => assignment.Bind(_options);
}
