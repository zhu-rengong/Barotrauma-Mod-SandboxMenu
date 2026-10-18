namespace SandboxMenu.Domain.Afflictions;

internal enum AfflictionApplyResult
{
    Applied,
    Blocked,
    NoCharacter
}

internal static class AfflictionApplier
{
    internal static AfflictionApplyResult Apply(
        Character character,
        AfflictionPrefab prefab,
        float strength,
        IReadOnlyList<int> limbs,
        bool multiplyByMaxVitality,
        Character? source)
    {
        if (character.Removed) { return AfflictionApplyResult.NoCharacter; }

        CharacterHealth health = character.CharacterHealth;

        if (character.GodMode || (!prefab.IsBuff && health.Unkillable)) { return AfflictionApplyResult.Blocked; }

        float clamped = Math.Clamp(strength, 0f, prefab.MaxStrength > 0f ? prefab.MaxStrength : 1f);

        if (multiplyByMaxVitality) { clamped *= character.MaxVitality / 100f; }

        if (limbs.Count == 0)
        {
            health.ApplyAffliction(null, prefab.Instantiate(clamped, source));

            return AfflictionApplyResult.Applied;
        }

        foreach (int index in limbs)
        {
            if (LimbOf(character, index) is not { } limb) { continue; }

            health.ApplyAffliction(limb, prefab.Instantiate(clamped, source));
        }

        return AfflictionApplyResult.Applied;
    }

    internal static int Remove(CharacterHealth health, AfflictionPrefab prefab, IReadOnlyList<int> limbs)
    {
        List<Affliction>? found = null;
        HashSet<Affliction>? removable = null;

        foreach (Affliction affliction in health.GetAllAfflictions())
        {
            if (affliction.Prefab != prefab || !Targets(health, affliction, limbs)) { continue; }

            (found ??= []).Add(affliction);

            affliction.Strength = 0f;

            if (!health.irremovableAfflictions.Contains(affliction))
            {
                (removable ??= new HashSet<Affliction>(ReferenceEqualityComparer.Instance)).Add(affliction);
            }
        }

        if (found is null) { return 0; }

        if (removable is not null) { health.RemoveAfflictions(affliction => removable.Contains(affliction)); }
        else { health.RecalculateVitality(); }

        return found.Count;
    }

    internal static Limb? LimbOf(Character character, int healthIndex)
    {
        if (character.AnimController is not { } controller) { return null; }

        Limb[] candidates = controller.Limbs;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].HealthIndex == healthIndex) { return candidates[i]; }
        }

        return null;
    }

    private static bool Targets(CharacterHealth health, Affliction affliction, IReadOnlyList<int> limbs)
    {
        if (limbs.Count == 0) { return true; }
        if (health.GetAfflictionLimb(affliction) is not { } limb) { return false; }

        for (int i = 0; i < limbs.Count; i++)
        {
            if (limbs[i] == limb.HealthIndex) { return true; }
        }

        return false;
    }
}
