namespace SandboxMenu.Domain.Spawning;

internal sealed class RemoteSpawnGateway : ISpawnGateway
{
    public bool IsRemote => true;

    public SpawnRefusal? IntoInventory(IReadOnlyList<SpawnEntry> entries, Character character)
        => ClientSpawnDispatcher.TrySendIntoInventory(entries, character.ID);

    public SpawnRefusal? ToWorld(IReadOnlyList<SpawnEntry> entries, Vector2 worldPosition, Character? character)
        => ClientSpawnDispatcher.TrySendToWorld(entries, worldPosition, character?.ID ?? 0);
}
