namespace SandboxMenu.Domain.Afflictions;

internal sealed class AfflictionGateway : IAfflictionGateway
{
    private readonly LocalAfflictionGateway _local = new();
    private readonly RemoteAfflictionGateway _remote = new();

    internal static AfflictionGateway Shared { get; } = new();

    public bool IsRemote => GameMain.NetworkMember is { IsClient: true };

    public AfflictionOutcome Apply(Character character, AfflictionPrefab prefab, float strength, AfflictionTargets targets)
        => Active.Apply(character, prefab, strength, targets);

    public AfflictionOutcome Remove(Character character, AfflictionPrefab prefab, int healthIndex)
        => Active.Remove(character, prefab, healthIndex);

    private IAfflictionGateway Active => IsRemote ? _remote : _local;
}
