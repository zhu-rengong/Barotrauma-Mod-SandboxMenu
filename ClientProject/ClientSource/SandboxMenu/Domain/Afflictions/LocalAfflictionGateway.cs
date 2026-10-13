namespace SandboxMenu.Domain.Afflictions;

internal sealed class LocalAfflictionGateway : IAfflictionGateway
{
    public bool IsRemote => false;

    public AfflictionOutcome Apply(Character character, AfflictionPrefab prefab, float strength, AfflictionTargets targets)
    {
        CharacterHealth health = character.CharacterHealth;

        if (character.GodMode || (!prefab.IsBuff && health.Unkillable)) { return AfflictionOutcome.Blocked; }

        if (targets.WholeBody)
        {
            health.ApplyAffliction(null, prefab.Instantiate(strength, Character.Controlled));
            return AfflictionOutcome.Applied;
        }

        foreach (Limb limb in targets.Limbs)
        {
            health.ApplyAffliction(limb, prefab.Instantiate(strength, Character.Controlled));
        }

        return AfflictionOutcome.Applied;
    }

    public AfflictionOutcome Remove(Character character, AfflictionPrefab prefab, int healthIndex)
    {
        AfflictionEditor.Clear(
            character.CharacterHealth,
            affliction => affliction.Prefab == prefab
                && (character.CharacterHealth.GetAfflictionLimb(affliction)?.HealthIndex ?? -1) == healthIndex);

        return AfflictionOutcome.Removed;
    }
}
