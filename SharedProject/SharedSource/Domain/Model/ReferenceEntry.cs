using System.Xml.Linq;

namespace SandboxMenu.Domain.Model;

// An entry that stands for another preset: what it spawns is whatever that preset holds.
internal sealed class ReferenceEntry : SpawnEntry
{
    public string TemplateName { get; set; } = string.Empty;

    public override XElement ToXml()
    {
        XElement element = new("Ref", new XAttribute("template", TemplateName));
        WriteCommon(element);
        return element;
    }

    internal static ReferenceEntry Read(XElement element)
    {
        ReferenceEntry entry = new()
        {
            TemplateName = element.GetAttributeString("template", string.Empty)
        };
        entry.ReadCommon(element);
        return entry;
    }

    protected override SpawnEntry CloneCore()
    {
        ReferenceEntry clone = new() { TemplateName = TemplateName };
        CopyCommonTo(clone);
        return clone;
    }
}
