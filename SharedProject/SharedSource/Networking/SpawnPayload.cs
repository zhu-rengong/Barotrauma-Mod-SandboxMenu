using System.Xml.Linq;

namespace SandboxMenu.Networking;

// One spawn request's worth of data: the entries to spawn plus every preset they refer to. Sending the referenced
// presets along is what lets the server expand template references without a preset folder of its own — and it
// keeps SpawnExecutor's expansion and cycle detection as the single implementation of that behaviour.
internal sealed class SpawnPayload
{
    internal const string RootName = "SpawnPayload";
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

    // Handed to SpawnExecutor, which resolves a reference by asking for its preset by name.
    internal SpawnSet? ResolveTemplate(string name)
        => _templates.TryGetValue(name, out SpawnSet? template) ? template : null;

    internal XElement ToXml()
    {
        var root = new XElement(RootName);

        if (_templates.Count > 0)
        {
            var templates = new XElement(TemplatesName);
            foreach (SpawnSet template in _templates.Values) { templates.Add(template.ToXml()); }

            root.Add(templates);
        }

        var entries = new XElement(EntriesName);
        foreach (SpawnEntry entry in Entries) { entries.Add(entry.ToXml()); }

        root.Add(entries);

        return root;
    }

    internal static SpawnPayload FromXml(XElement root)
    {
        var payload = new SpawnPayload();

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

    // Counts the entries a request would run, nested ones included, and gives up as soon as it is past the limit:
    // this is what stops a request from turning into unbounded work for the server.
    internal bool WithinEntryLimit()
    {
        int count = 0;
        var pending = new Stack<IEnumerable<SpawnEntry>>();
        pending.Push(Entries);

        while (pending.Count > 0)
        {
            foreach (SpawnEntry entry in pending.Pop())
            {
                if (++count > SpawnPayloadCodec.MaxEntries) { return false; }
                if (entry is ItemEntry { Inventory.Count: > 0 } item) { pending.Push(item.Inventory); }
            }
        }

        return true;
    }
}
