namespace SandboxMenu.Domain.Prefabs;

internal static class ItemPrefabLookup
{
    internal static ItemPrefab? By(string? identifier)
        => !string.IsNullOrWhiteSpace(identifier) && ItemPrefab.Prefabs.TryGet(Identifiers.Of(identifier), out ItemPrefab? prefab)
            ? prefab
            : null;
}
