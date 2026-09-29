using System.Xml;
using System.Xml.Linq;

namespace UiFramework;

internal sealed class MarkupAttribute(string name, MarkupValue value)
{
    internal string Name { get; } = name;

    internal MarkupValue Value { get; } = value;
}

internal sealed class MarkupNode
{
    private readonly Dictionary<string, MarkupAttribute> _attributes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MarkupNode> _children = [];

    private MarkupNode(string name, string? owner, string view, int line)
    {
        Name = name;
        Owner = owner;
        View = view;
        Line = line;
    }

    internal string Name { get; }

    internal string? Owner { get; }

    internal string View { get; }

    internal int Line { get; }

    internal bool IsProperty => Owner is not null;

    internal IReadOnlyCollection<MarkupAttribute> Attributes => _attributes.Values;

    internal IReadOnlyList<MarkupNode> Children => _children;

    internal IEnumerable<MarkupNode> Content => _children.Where(child => !child.IsProperty);

    internal string? ElementName => Text("Name");

    internal MarkupAttribute? Attribute(string name) => _attributes.GetValueOrDefault(name);

    internal MarkupValue? Value(string name) => Attribute(name)?.Value;

    internal string? Text(string name) => Attribute(name)?.Value.Raw;

    internal static MarkupNode Parse(string view, XElement element)
    {
        (string name, string? owner) = SplitName(element.Name.LocalName);

        MarkupNode node = new(name, owner, view, LineOf(element));

        foreach (XAttribute attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration) { continue; }

            node._attributes[attribute.Name.LocalName] = new MarkupAttribute(attribute.Name.LocalName, MarkupValue.Parse(attribute.Value));
        }

        foreach (XElement child in element.Elements())
        {
            node._children.Add(Parse(view, child));
        }

        return node;
    }

    public override string ToString() => $"{View}:{Line} <{Name}>";

    private static (string Name, string? Owner) SplitName(string localName)
    {
        int dot = localName.IndexOf('.', StringComparison.Ordinal);
        return dot <= 0 ? (localName, null) : (localName[(dot + 1)..], localName[..dot]);
    }

    private static int LineOf(XElement element)
        => element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
}
