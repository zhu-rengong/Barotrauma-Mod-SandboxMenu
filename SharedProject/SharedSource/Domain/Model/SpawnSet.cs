using System.Xml.Linq;

namespace SandboxMenu.Domain.Model;

public sealed class SpawnSet
{
    public string Name { get; set; } = string.Empty;

    public List<SpawnEntry> Entries { get; } = [];

    public XElement ToXml()
    {
        var element = new XElement("SpawnSet", new XAttribute("name", Name));
        foreach (SpawnEntry entry in Entries)
        {
            element.Add(entry.ToXml());
        }
        return element;
    }

    public static SpawnSet FromXml(XElement element)
    {
        var set = new SpawnSet
        {
            Name = element.GetAttributeString("name", "default")
        };

        foreach (XElement child in element.Elements())
        {
            SpawnEntry? entry = SpawnEntry.FromXml(child);
            if (entry is not null) { set.Entries.Add(entry); continue; }

            Log.Warn($"Skipped unsupported spawn entry '{child.Name.LocalName}' in preset '{set.Name}'.");
        }

        return set;
    }

    public SpawnSet Clone()
    {
        var clone = new SpawnSet { Name = Name };
        foreach (SpawnEntry entry in Entries) { clone.Entries.Add(entry.Clone()); }
        return clone;
    }
}
