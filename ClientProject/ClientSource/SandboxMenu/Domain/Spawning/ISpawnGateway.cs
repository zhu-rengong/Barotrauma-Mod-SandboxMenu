namespace SandboxMenu.Domain.Spawning;

internal interface ISpawnGateway
{
    bool IsRemote { get; }

    SpawnRefusal? IntoInventory(IReadOnlyList<SpawnEntry> entries, Character character);

    SpawnRefusal? ToWorld(IReadOnlyList<SpawnEntry> entries, Vector2 worldPosition, Character? character);
}
