namespace SandboxMenu.Domain.Prefabs;

internal static class ItemPrefabLookup
{
    internal static ItemPrefab? By(string? identifier)
        => !string.IsNullOrWhiteSpace(identifier) && ItemPrefab.Prefabs.TryGet(identifier.ToIdentifier(), out ItemPrefab? prefab)
            ? prefab
            : null;
}
