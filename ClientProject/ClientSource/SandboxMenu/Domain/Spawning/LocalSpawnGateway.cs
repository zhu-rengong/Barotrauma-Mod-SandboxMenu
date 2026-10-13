namespace SandboxMenu.Domain.Spawning;

internal sealed class LocalSpawnGateway : ISpawnGateway
{
    public bool IsRemote => false;

    public SpawnRefusal? IntoInventory(IReadOnlyList<SpawnEntry> entries, Character character)
    {
        ItemSpawnService.SpawnIntoInventory(entries, character, Resolve);

        return null;
    }

    public SpawnRefusal? ToWorld(IReadOnlyList<SpawnEntry> entries, Vector2 worldPosition, Character? character)
    {
        ItemSpawnService.SpawnAtWorld(entries, worldPosition, character, Resolve);

        return null;
    }

    private static SpawnSet? Resolve(string name) => TemplateStore.TryLoad(name, out SpawnSet? set) ? set : null;
}
