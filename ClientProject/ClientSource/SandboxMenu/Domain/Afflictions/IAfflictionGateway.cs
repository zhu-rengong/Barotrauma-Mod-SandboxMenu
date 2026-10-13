namespace SandboxMenu.Domain.Afflictions;

internal enum AfflictionOutcome
{
    Sent,
    Applied,
    Removed,
    Blocked,
    NotSent
}

internal readonly record struct AfflictionTargets(bool WholeBody, IReadOnlyList<Limb> Limbs);

internal interface IAfflictionGateway
{
    bool IsRemote { get; }

    AfflictionOutcome Apply(Character character, AfflictionPrefab prefab, float strength, AfflictionTargets targets);

    AfflictionOutcome Remove(Character character, AfflictionPrefab prefab, int healthIndex);
}
