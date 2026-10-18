namespace SandboxMenu.UI.ViewModels;

internal static class EntryTree
{
    internal static bool Contains(SpawnEntry ancestor, SpawnEntry candidate, IReadOnlyDictionary<SpawnEntry, ItemEntry> parents)
    {
        if (ReferenceEquals(ancestor, candidate)) { return true; }
        if (ancestor is not ItemEntry) { return false; }

        return BranchUnder((ItemEntry)ancestor, candidate, parents) is not null;
    }

    internal static bool LiftBranch(
        List<SpawnEntry> owner,
        ItemEntry moved,
        SpawnEntry descendant,
        IReadOnlyDictionary<SpawnEntry, ItemEntry> parents)
    {
        SpawnEntry? branch = BranchUnder(moved, descendant, parents);
        if (branch is null) { return false; }

        int from = owner.IndexOf(moved);
        if (from < 0) { return false; }

        owner.RemoveAt(from);
        owner.Insert(Math.Min(from, owner.Count), branch);

        moved.Inventory.Remove(branch);

        return true;
    }

    internal static bool ReparentIntoDescendant(
        List<SpawnEntry> owner,
        ItemEntry moved,
        ItemEntry container,
        IReadOnlyDictionary<SpawnEntry, ItemEntry> parents)
    {
        if (!LiftBranch(owner, moved, container, parents)) { return false; }

        container.Inventory.Add(moved);

        return true;
    }

    private static SpawnEntry? BranchUnder(ItemEntry ancestor, SpawnEntry candidate, IReadOnlyDictionary<SpawnEntry, ItemEntry> parents)
    {
        SpawnEntry current = candidate;

        while (parents.TryGetValue(current, out ItemEntry? container))
        {
            if (ReferenceEquals(container, ancestor)) { return current; }

            current = container;
        }

        return null;
    }
}
