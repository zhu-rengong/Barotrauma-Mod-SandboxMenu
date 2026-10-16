namespace SandboxMenu.UI.ViewModels;

internal static class SpawnTree
{
    internal static bool Contains(SpawnEntry ancestor, SpawnEntry candidate)
    {
        if (ReferenceEquals(ancestor, candidate)) { return true; }
        if (ancestor is not ItemEntry item) { return false; }

        foreach (SpawnEntry child in item.Inventory)
        {
            if (Contains(child, candidate)) { return true; }
        }

        return false;
    }

    internal static bool LiftBranch(List<SpawnEntry> owner, ItemEntry moved, SpawnEntry descendant)
    {
        SpawnEntry? branch = PathChildOf(moved, descendant);
        if (branch is null) { return false; }

        int from = owner.IndexOf(moved);
        if (from < 0) { return false; }

        owner.RemoveAt(from);
        owner.Insert(Math.Min(from, owner.Count), branch);

        moved.Inventory.Remove(branch);

        return true;
    }

    internal static bool ReparentIntoDescendant(List<SpawnEntry> owner, ItemEntry moved, ItemEntry container)
    {
        if (!LiftBranch(owner, moved, container)) { return false; }

        container.Inventory.Add(moved);

        return true;
    }

    private static SpawnEntry? PathChildOf(ItemEntry ancestor, SpawnEntry candidate)
    {
        foreach (SpawnEntry child in ancestor.Inventory)
        {
            if (Contains(child, candidate)) { return child; }
        }

        return null;
    }
}
