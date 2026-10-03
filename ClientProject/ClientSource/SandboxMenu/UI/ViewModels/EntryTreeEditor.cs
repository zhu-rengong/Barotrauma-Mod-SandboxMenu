namespace SandboxMenu.UI.ViewModels;

// Everything that changes the spawn set behind the tree: adding, duplicating, deleting, moving and dropping entries,
// and which row the tree shows as selected afterwards.
internal sealed class EntryTreeEditor(SpawnMenuViewModel menu)
{
    private readonly SpawnSet _set = menu.Set;
    private readonly SpawnSetTree _tree = menu.Tree;

    internal void AddItem(bool asChild)
    {
        TreeEntryViewModel? selected = menu.SelectedEntry;

        if (asChild && selected is not null) { AddChild(selected); return; }
        if (asChild) { return; }

        Add(new ItemEntry(), selected);
    }

    internal void DeleteSelected()
    {
        if (menu.SelectedEntry is { } selected) { Delete(selected); }
    }

    internal void Add(SpawnEntry entry, TreeEntryViewModel? after)
    {
        List<SpawnEntry> owner = after?.Owner ?? _set.Entries;
        int index = after is null ? owner.Count : Math.Clamp(owner.IndexOf(after.Entry) + 1, 0, owner.Count);

        owner.Insert(index, entry);
        SelectNewEntry(entry, owner);
    }

    internal void AddChild(TreeEntryViewModel row)
    {
        if (row.Entry is not ItemEntry item) { return; }

        ItemEntry child = new();
        item.Inventory.Add(child);
        SelectNewEntry(child, item.Inventory);
    }

    internal void Duplicate(TreeEntryViewModel row)
    {
        SpawnEntry clone = row.Entry.Clone();
        int index = row.Owner.IndexOf(row.Entry);
        row.Owner.Insert(index < 0 ? row.Owner.Count : index + 1, clone);

        SelectNewEntry(clone, row.Owner);
    }

    internal void Delete(TreeEntryViewModel row)
    {
        int index = row.Owner.IndexOf(row.Entry);
        if (index < 0) { return; }

        row.Owner.RemoveAt(index);

        // The entry that took its place, or the item it was stored in once it was the last one of its kind.
        SpawnEntry? next = row.Owner.Count > 0
            ? row.Owner[Math.Min(index, row.Owner.Count - 1)]
            : row.Container;

        FrameActions.Post(() =>
        {
            _tree.Rebuild();

            if (next is not null && _tree.Find(next) is { } selected) { menu.Select(selected); }
            else { menu.ClearSelection(); }
        });
    }

    // The order is the spawn order, so moving is how an entry is put before or after its neighbours.
    internal void Move(TreeEntryViewModel row, int direction)
    {
        List<SpawnEntry> owner = row.Owner;
        int index = owner.IndexOf(row.Entry);
        int target = index + direction;

        if (index < 0 || target < 0 || target >= owner.Count) { return; }

        (owner[index], owner[target]) = (owner[target], owner[index]);

        SelectNewEntry(row.Entry, owner);
    }

    internal void MoveSelection(int direction)
    {
        if (menu.SelectedEntry is { } selected) { Move(selected, direction); }
    }

    // Previous and next walk the list as it is shown, so they cross levels; moving stays inside one list.
    internal void Step(TreeEntryViewModel row, int direction)
    {
        int index = _tree.Rows.IndexOf(row);
        int target = index + direction;

        if (index < 0 || target < 0 || target >= _tree.Rows.Count) { return; }

        menu.Select(_tree.Rows[target]);
    }

    internal void StepSelection(int direction)
    {
        if (menu.SelectedEntry is { } selected) { Step(selected, direction); }
    }

    internal void Drop(object source, object? target, DropMode mode)
    {
        if (source is not TreeEntryViewModel dragged) { return; }

        TreeEntryViewModel? onto = target as TreeEntryViewModel;
        if (onto is not null && SpawnTree.Contains(dragged.Entry, onto.Entry))
        {
            DropIntoDescendant(dragged, onto, mode);
            return;
        }

        List<SpawnEntry> owner;
        int index;

        if (onto is null)
        {
            owner = _set.Entries;
            index = owner.Count;
        }
        else if (mode == DropMode.Nest && onto.Entry is ItemEntry container)
        {
            owner = container.Inventory;
            index = owner.Count;
        }
        else
        {
            owner = onto.Owner;
            index = owner.IndexOf(onto.Entry) + (mode == DropMode.After ? 1 : 0);
        }

        bool sameList = ReferenceEquals(owner, dragged.Owner);
        int from = dragged.Owner.IndexOf(dragged.Entry);
        if (from < 0) { return; }

        dragged.Owner.RemoveAt(from);
        if (sameList && from < index) { index--; }

        owner.Insert(Math.Clamp(index, 0, owner.Count), dragged.Entry);
        SelectNewEntry(dragged.Entry, owner);
    }

    private void DropIntoDescendant(TreeEntryViewModel dragged, TreeEntryViewModel onto, DropMode mode)
    {
        if (dragged.Entry is not ItemEntry moved || ReferenceEquals(dragged, onto)) { return; }

        if (mode == DropMode.Nest)
        {
            if (onto.Entry is ItemEntry container && SpawnTree.ReparentIntoDescendant(dragged.Owner, moved, container))
            {
                SelectNewEntry(moved, container.Inventory);
            }

            return;
        }

        bool liftedTarget = ReferenceEquals(onto.Owner, moved.Inventory);
        if (!liftedTarget && onto.Owner.IndexOf(onto.Entry) < 0) { return; }

        if (!SpawnTree.LiftBranch(dragged.Owner, moved, onto.Entry)) { return; }

        List<SpawnEntry> owner = liftedTarget ? dragged.Owner : onto.Owner;
        int index = owner.IndexOf(onto.Entry);
        if (index < 0) { return; }

        owner.Insert(Math.Clamp(index + (mode == DropMode.After ? 1 : 0), 0, owner.Count), moved);
        SelectNewEntry(moved, owner);
    }

    private void SelectNewEntry(SpawnEntry entry, List<SpawnEntry> owner)
    {
        FrameActions.Post(() =>
        {
            _tree.Rebuild();

            if (_tree.Find(entry, owner) is { } row) { menu.Select(row); }
        });
    }
}
