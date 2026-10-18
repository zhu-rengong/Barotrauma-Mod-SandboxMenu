using System.Xml.Linq;

namespace SandboxMenu.Networking;

internal sealed class SpawnMessage
{
    internal const string RootName = "SpawnMessage";
    internal const string EntriesName = "Entries";
    internal const string TemplatesName = "Templates";

    private readonly Dictionary<string, SpawnSet> _templates = new(StringComparer.OrdinalIgnoreCase);

    internal List<SpawnEntry> Entries { get; } = [];

    internal int TemplateCount => _templates.Count;

    internal void AddTemplate(SpawnSet template)
    {
        if (string.IsNullOrEmpty(template.Name)) { return; }

        _templates[template.Name] = template;
    }

    internal SpawnSet? ResolveTemplate(string name)
        => _templates.TryGetValue(name, out SpawnSet? template) ? template : null;

    internal XElement ToXml()
    {
        XElement root = new(RootName);

        if (_templates.Count > 0)
        {
            XElement templates = new(TemplatesName);
            foreach (SpawnSet template in _templates.Values) { templates.Add(template.ToXml()); }

            root.Add(templates);
        }

        XElement entries = new(EntriesName);
        foreach (SpawnEntry entry in Entries) { entries.Add(entry.ToXml()); }

        root.Add(entries);

        return root;
    }

    internal static SpawnMessage FromXml(XElement root)
    {
        SpawnMessage payload = new();

        if (root.Element(TemplatesName) is { } templates)
        {
            foreach (XElement element in templates.Elements()) { payload.AddTemplate(SpawnSet.FromXml(element)); }
        }

        if (root.Element(EntriesName) is { } entries)
        {
            foreach (XElement element in entries.Elements())
            {
                if (SpawnEntry.FromXml(element) is { } entry) { payload.Entries.Add(entry); }
            }
        }

        return payload;
    }

    internal bool WithinEntryLimit()
    {
        int count = 0;
        Stack<IEnumerable<SpawnEntry>> pending = new();
        pending.Push(Entries);

        while (pending.Count > 0)
        {
            foreach (SpawnEntry entry in pending.Pop())
            {
                if (++count > SpawnMessageCodec.MaxEntries) { return false; }
                if (entry is ItemEntry { Inventory.Count: > 0 } item) { pending.Push(item.Inventory); }
            }
        }

        return true;
    }
}
