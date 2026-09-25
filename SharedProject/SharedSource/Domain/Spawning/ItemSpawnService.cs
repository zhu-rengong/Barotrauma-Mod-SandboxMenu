namespace SandboxMenu.Domain.Spawning;

public static class ItemSpawnService
{
    public static int SpawnIntoInventory(IReadOnlyList<SpawnEntry> entries, Character character, Func<string, SpawnSet?>? resolveTemplate)
        => Spawn(InventoryTarget(character), entries, resolveTemplate).QueuedCount;

    public static int SpawnAtWorld(IReadOnlyList<SpawnEntry> entries, Vector2 worldPosition, Character? character, Func<string, SpawnSet?>? resolveTemplate)
        => Spawn(new SpawnTarget.AtWorld(worldPosition, character), entries, resolveTemplate).QueuedCount;

    // What became of a spawn is the server's business as much as the count is — it is the one that reports back to
    // the player who asked — so this hands over the whole result.
    internal static SpawnResult Spawn(SpawnTarget? target, IReadOnlyList<SpawnEntry> entries, Func<string, SpawnSet?>? resolveTemplate)
    {
        if (target is null) { return new SpawnResult(); }

        var executor = new SpawnExecutor(target, resolveTemplate);
        executor.Run(entries);

        return executor.Result;
    }

    internal static SpawnTarget? InventoryTarget(Character character)
        => character.Inventory is { } inventory ? new SpawnTarget.IntoInventory(inventory, character) : null;
}
