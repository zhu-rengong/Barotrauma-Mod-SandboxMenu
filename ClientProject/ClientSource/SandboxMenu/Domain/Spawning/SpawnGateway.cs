namespace SandboxMenu.Domain.Spawning;

internal sealed class SpawnGateway : ISpawnGateway
{
    private readonly LocalSpawnGateway _local = new();
    private readonly RemoteSpawnGateway _remote = new();

    internal static SpawnGateway Shared { get; } = new();

    public bool IsRemote => GameMain.NetworkMember is { IsClient: true };

    public SpawnRefusal? IntoInventory(IReadOnlyList<SpawnEntry> entries, Character character)
        => Active.IntoInventory(entries, character);

    public SpawnRefusal? ToWorld(IReadOnlyList<SpawnEntry> entries, Vector2 worldPosition, Character? character)
        => Active.ToWorld(entries, worldPosition, character);

    private ISpawnGateway Active => IsRemote ? _remote : _local;
}
