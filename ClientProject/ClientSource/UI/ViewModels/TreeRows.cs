using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

internal sealed class TreeRows(
    SpawnSet set,
    Action<TreeEntryViewModel> select,
    Action<TreeEntryViewModel> showContextMenu)
{
    private readonly ObservableCollection<TreeEntryViewModel> _rows = [];
    private readonly Dictionary<TreeEntryViewModel, int> _index = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<SpawnEntry, ItemEntry> _parents = new(ReferenceEqualityComparer.Instance);

    internal ObservableCollection<TreeEntryViewModel> Rows => _rows;

    internal IReadOnlyDictionary<SpawnEntry, ItemEntry> Parents => _parents;

    internal bool Contains(SpawnEntry ancestor, SpawnEntry candidate) => EntryTree.Contains(ancestor, candidate, _parents);

    internal int IndexOf(TreeEntryViewModel row) => _index.GetValueOrDefault(row, -1);

    internal void Rebuild()
    {
        _rows.Clear();
        _index.Clear();
        _parents.Clear();
        Flatten(set.Entries, 0, null);
    }

    internal TreeEntryViewModel? Find(SpawnEntry entry, List<SpawnEntry>? owner = null)
    {
        if (owner is not null
            && _rows.FirstOrDefault(row => ReferenceEquals(row.Entry, entry) && ReferenceEquals(row.Owner, owner)) is { } exact)
        {
            return exact;
        }

        return _rows.FirstOrDefault(row => ReferenceEquals(row.Entry, entry));
    }

    internal void Refresh()
    {
        foreach (TreeEntryViewModel row in _rows) { row.Refresh(); }
    }

    internal void Refresh(SpawnEntry entry)
    {
        foreach (TreeEntryViewModel row in _rows)
        {
            if (ReferenceEquals(row.Entry, entry)) { row.Refresh(); }
        }
    }

    internal void RefreshHints()
    {
        foreach (TreeEntryViewModel row in _rows) { row.RefreshHints(); }
    }

    internal void Release()
    {
        foreach (TreeEntryViewModel row in _rows) { row.Release(); }
    }

    private void Flatten(List<SpawnEntry> entries, int depth, ItemEntry? container)
    {
        foreach (SpawnEntry entry in entries)
        {
            TreeEntryViewModel row = new(entry, entries, depth, container, select, showContextMenu);

            _index[row] = _rows.Count;
            _rows.Add(row);

            if (container is not null) { _parents[entry] = container; }

            if (entry is ItemEntry { Inventory.Count: > 0 } item)
            {
                Flatten(item.Inventory, depth + 1, item);
            }
        }
    }
}
