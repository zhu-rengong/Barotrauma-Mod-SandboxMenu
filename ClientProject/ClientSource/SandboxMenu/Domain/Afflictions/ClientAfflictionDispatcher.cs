namespace SandboxMenu.Domain.Afflictions;

internal enum AfflictionOutcome
{
    Sent,
    Applied,
    Removed,
    Blocked,
    NotSent
}

internal enum AfflictionRefusal
{
    NotSent
}

internal readonly record struct AfflictionTargets(bool WholeBody, IReadOnlyList<Limb> Limbs);

internal static class ClientAfflictionDispatcher
{
    private static uint _nextRequestId;
    private static uint _waitingFor;

    internal static bool IsRemote => GameMain.NetworkMember is { IsClient: true };

    internal static AfflictionOutcome Apply(Character character, AfflictionPrefab prefab, float strength, AfflictionTargets targets)
        => IsRemote
            ? Sent(TrySend(character, prefab, strength, LimbsOf(targets), AfflictionMode.Apply))
            : ApplyLocally(character, prefab, strength, targets);

    internal static AfflictionOutcome Remove(Character character, AfflictionPrefab prefab, int healthIndex)
        => IsRemote
            ? Sent(TrySend(character, prefab, 0f, [healthIndex], AfflictionMode.Remove))
            : RemoveLocally(character, prefab, healthIndex);

    internal static void OnResponse(AfflictionResponse response)
    {
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        _waitingFor = 0;

        MenuWindow.Current?.ViewModel.Afflictions.ApplyResult((AfflictionStatus)response.Status);
    }

    private static AfflictionOutcome ApplyLocally(Character character, AfflictionPrefab prefab, float strength, AfflictionTargets targets)
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

    private static AfflictionOutcome RemoveLocally(Character character, AfflictionPrefab prefab, int healthIndex)
    {
        AfflictionEditor.Clear(
            character.CharacterHealth,
            affliction => affliction.Prefab == prefab
                && (character.CharacterHealth.GetAfflictionLimb(affliction)?.HealthIndex ?? -1) == healthIndex);

        return AfflictionOutcome.Removed;
    }

    private static AfflictionRefusal? TrySend(
        Character character,
        AfflictionPrefab prefab,
        float strength,
        int[] limbs,
        AfflictionMode mode)
    {
        _waitingFor = 0;

        uint requestId = ++_nextRequestId;

        AfflictionRequest request = new(
            prefab.UintIdentifier,
            strength,
            limbs,
            (byte)mode,
            character.ID,
            requestId);

        try
        {
            Services.GameNetwork.Send(NetworkHeaders.AfflictionRequest, request);
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Sending the affliction request failed: {e}");
            return AfflictionRefusal.NotSent;
        }

        _waitingFor = requestId;

        return null;
    }

    private static int[] LimbsOf(AfflictionTargets targets)
        => targets.WholeBody ? [] : [.. targets.Limbs.Select(limb => limb.HealthIndex)];

    private static AfflictionOutcome Sent(AfflictionRefusal? refusal)
        => refusal is null ? AfflictionOutcome.Sent : AfflictionOutcome.NotSent;
}
