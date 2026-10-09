using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

internal sealed class SpawnSetTree(SpawnPanelViewModel menu, SpawnSet set)
{
    private readonly ObservableCollection<TreeEntryViewModel> _rows = [];

    internal ObservableCollection<TreeEntryViewModel> Rows => _rows;

    internal void Rebuild()
    {
        _rows.Clear();
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

    internal void RefreshSummaries()
    {
        foreach (TreeEntryViewModel row in _rows) { row.Refresh(); }
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
            _rows.Add(new TreeEntryViewModel(menu, entry, entries, depth, container));

            if (entry is ItemEntry { Inventory.Count: > 0 } item)
            {
                Flatten(item.Inventory, depth + 1, item);
            }
        }
    }
}
