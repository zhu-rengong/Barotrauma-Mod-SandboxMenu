using System.Xml.Linq;

namespace SandboxMenu.Domain.Model;

internal sealed class RefEntry : SpawnEntry
{
    public string TemplateName { get; set; } = string.Empty;

#if CLIENT
    public override LocalizedString Summary => $"@ {TemplateName}";
#endif

    public override XElement ToXml()
    {
        XElement element = new("Ref", new XAttribute("template", TemplateName));
        WriteCommon(element);
        return element;
    }

    internal static RefEntry Read(XElement element)
    {
        RefEntry entry = new()
        {
            TemplateName = element.GetAttributeString("template", string.Empty)
        };
        entry.ReadCommon(element);
        return entry;
    }

    protected override SpawnEntry CloneCore()
    {
        RefEntry clone = new() { TemplateName = TemplateName };
        CopyCommonTo(clone);
        return clone;
    }
}
