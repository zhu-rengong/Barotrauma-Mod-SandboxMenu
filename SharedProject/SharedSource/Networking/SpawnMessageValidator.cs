namespace SandboxMenu.Networking;

internal static class SpawnMessageValidator
{
    internal static bool TryValidate(SpawnRequest request, SpawnMessage payload, out string problem)
    {
        if (request.Target is not ((byte)SpawnTargetKind.Inventory) and not ((byte)SpawnTargetKind.World))
        {
            problem = $"unknown spawn target {request.Target}";
            return false;
        }

        if (request.Target == (byte)SpawnTargetKind.World && !IsFinite(request.Position))
        {
            problem = "the spawn position is not a finite vector";
            return false;
        }

        foreach (SpawnSet template in payload.Templates)
        {
            if (!TryValidateEntries(template.Entries, out problem)) { return false; }
        }

        if (!TryValidateEntries(payload.Entries, out problem)) { return false; }

        problem = string.Empty;
        return true;
    }

    internal static bool TryValidate(AfflictionRequest request, out string problem)
    {
        if (request.Mode is not ((byte)AfflictionMode.Apply) and not ((byte)AfflictionMode.Remove))
        {
            problem = $"unknown affliction mode {request.Mode}";
            return false;
        }

        if (!float.IsFinite(request.Strength) || request.Strength < 0f)
        {
            problem = "the affliction strength is not a finite positive number";
            return false;
        }

        problem = string.Empty;
        return true;
    }

    private static bool TryValidateEntries(IReadOnlyList<SpawnEntry> entries, out string problem)
    {
        problem = string.Empty;

        foreach (SpawnEntry entry in entries)
        {
            switch (entry)
            {
                case ReferenceEntry reference:
                    if (!IsFiniteRange(reference.Amount))
                    {
                        problem = $"template '{reference.TemplateName}' has an unusable amount";
                        return false;
                    }

                    break;

                case ItemEntry item:
                    if (!TryValidateItem(item, out problem)) { return false; }
                    break;
            }
        }

        return true;
    }

    private static bool TryValidateItem(ItemEntry entry, out string problem)
    {
        problem = string.Empty;

        if (!IsFiniteRange(entry.Amount) || !IsFiniteRange(entry.Stacks))
        {
            problem = $"'{entry.Identifier}' has an unusable amount";
            return false;
        }

        return TryValidateEntries(entry.Inventory, out problem);
    }

    private static bool IsFiniteRange(ValueRange? range)
        => range is not { } value
            || (float.IsFinite(value.Min) && float.IsFinite(value.Max));

    private static bool IsFinite(Vector2 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
