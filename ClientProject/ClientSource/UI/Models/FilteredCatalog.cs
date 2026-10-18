namespace SandboxMenu.UI.Models;

internal sealed class FilteredCatalog<TEntry, TRow>(IReadOnlyList<TEntry> entries) where TRow : class
{
    private readonly TRow?[] _built = new TRow?[entries.Count];

    internal int Count => entries.Count;

    internal IReadOnlyList<TRow> Select(Func<TEntry, bool> shows, Func<TEntry, TRow> build)
    {
        List<TRow> visible = new(entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            TEntry entry = entries[i];

            if (!shows(entry)) { continue; }

            visible.Add(_built[i] ??= build(entry));
        }

        return visible;
    }
}
