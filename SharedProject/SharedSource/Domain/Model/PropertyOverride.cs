using System.Xml.Linq;

namespace SandboxMenu.Domain.Model;

internal sealed class PropertyOverride
{
    public string ComponentName { get; set; } = string.Empty;

    public int ComponentIndex { get; set; } = 1;

    public string PropertyName { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    // A row whose property has not been picked yet is a draft: it names nothing the engine could set, so a preset
    // must neither write it out nor read it back (the editor still keeps it, and it fills in once it is named).
    public bool IsNamed => !string.IsNullOrEmpty(PropertyName);

    public PropertyOverride Clone() => (PropertyOverride)MemberwiseClone();

    public XElement ToXml() => new(
        "Property",
        new XAttribute("component", ComponentName),
        new XAttribute("index", XmlValue.Text(ComponentIndex)),
        new XAttribute("name", PropertyName),
        new XAttribute("value", Value));

    public static PropertyOverride FromXml(XElement element) => new()
    {
        ComponentName = element.GetAttributeString("component", string.Empty),
        ComponentIndex = element.GetAttributeInt("index", 1),
        PropertyName = element.GetAttributeString("name", string.Empty),
        Value = element.GetAttributeString("value", string.Empty)
    };

    public override string ToString()
        => string.IsNullOrEmpty(ComponentName)
            ? $"{PropertyName} = {Value}"
            : $"{ComponentName}[{ComponentIndex}].{PropertyName} = {Value}";
}
