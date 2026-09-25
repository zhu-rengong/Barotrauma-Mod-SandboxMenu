using System.Xml.Linq;

namespace SandboxMenu.Domain.Model;

public sealed class RefEntry : SpawnEntry
{
    public string TemplateName { get; set; } = string.Empty;

#if CLIENT
    public override LocalizedString Summary => $"@ {TemplateName}";
#endif

    public override XElement ToXml()
    {
        var element = new XElement("Ref", new XAttribute("template", TemplateName));
        WriteCommon(element);
        return element;
    }

    internal static RefEntry Read(XElement element)
    {
        var entry = new RefEntry
        {
            TemplateName = element.GetAttributeString("template", string.Empty)
        };
        entry.ReadCommon(element);
        return entry;
    }

    protected override SpawnEntry CloneCore()
    {
        var clone = new RefEntry { TemplateName = TemplateName };
        CopyCommonTo(clone);
        return clone;
    }
}
