namespace SandboxMenu.Domain.Afflictions;

internal sealed class RemoteAfflictionGateway : IAfflictionGateway
{
    public bool IsRemote => true;

    public AfflictionOutcome Apply(Character character, AfflictionPrefab prefab, float strength, AfflictionTargets targets)
        => Outcome(ClientAfflictionDispatcher.TrySend(character, prefab, strength, LimbsOf(targets), AfflictionMode.Apply));

    public AfflictionOutcome Remove(Character character, AfflictionPrefab prefab, int healthIndex)
        => Outcome(ClientAfflictionDispatcher.TrySend(character, prefab, 0f, [healthIndex], AfflictionMode.Remove));

    private static int[] LimbsOf(AfflictionTargets targets)
        => targets.WholeBody ? [] : [.. targets.Limbs.Select(limb => limb.HealthIndex)];

    private static AfflictionOutcome Outcome(AfflictionRefusal? refusal)
        => refusal is null ? AfflictionOutcome.Sent : AfflictionOutcome.NotSent;
}
