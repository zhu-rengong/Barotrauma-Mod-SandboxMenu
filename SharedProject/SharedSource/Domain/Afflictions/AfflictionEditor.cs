namespace SandboxMenu.Domain.Afflictions;

internal static class AfflictionEditor
{
    internal static int Clear(CharacterHealth health, Func<Affliction, bool> matches)
    {
        List<Affliction>? found = null;
        HashSet<Affliction>? removable = null;

        foreach (Affliction affliction in health.GetAllAfflictions())
        {
            if (!matches(affliction)) { continue; }

            (found ??= []).Add(affliction);

            affliction.Strength = 0f;

            if (!health.irremovableAfflictions.Contains(affliction))
            {
                (removable ??= new HashSet<Affliction>(ReferenceEqualityComparer.Instance)).Add(affliction);
            }
        }

        if (found is null) { return 0; }

        if (removable is not null)
        {
            health.RemoveAfflictions(affliction => removable.Contains(affliction));
        }
        else
        {
            health.RecalculateVitality();
        }

        return found.Count;
    }
}
