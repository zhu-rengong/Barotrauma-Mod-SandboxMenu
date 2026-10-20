namespace SandboxMenu.Domain.Afflictions;

internal enum AfflictionOutcome
{
    Sent,
    Applied,
    Removed,
    Blocked,
    Denied,
    NotSent
}

internal readonly record struct AfflictionTargets(bool WholeBody, IReadOnlyList<Limb> Limbs);

internal static class ClientAfflictionDispatcher
{
    private const double Timeout = 3.0;

    private static uint _nextRequestId;
    private static uint _waitingFor;
    private static double _sentAt;

    internal static event Action<AfflictionStatus>? Settled;

    internal static bool IsRemote => GameMain.NetworkMember is { IsClient: true };

    internal static AfflictionOutcome Apply(Character character, AfflictionPrefab prefab, float strength, AfflictionTargets targets, bool multiplyByMaxVitality)
        => IsRemote
            ? Send(character, prefab, strength, LimbsOf(targets), AfflictionMode.Apply, multiplyByMaxVitality)
            : ApplyLocally(character, prefab, strength, targets, multiplyByMaxVitality);

    internal static AfflictionOutcome Remove(Character character, AfflictionPrefab prefab, int? limbHealthIndex)
        => IsRemote
            ? Send(character, prefab, 0f, LimbIndexes(limbHealthIndex), AfflictionMode.Remove, false)
            : RemoveLocally(character, prefab, limbHealthIndex);

    internal static void Tick()
    {
        if (_waitingFor == 0 || Timing.TotalTime - _sentAt < Timeout) { return; }

        _waitingFor = 0;

        Settled?.Invoke(AfflictionStatus.NoResponse);
    }

    internal static void OnResponse(AfflictionResponse response)
    {
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        _waitingFor = 0;

        Settled?.Invoke((AfflictionStatus)response.Status);
    }

    private static AfflictionOutcome ApplyLocally(Character character, AfflictionPrefab prefab, float strength, AfflictionTargets targets, bool multiplyByMaxVitality)
    {
        AfflictionApplyResult applied = AfflictionApplier.Apply(
            character, prefab, strength, LimbsOf(targets), multiplyByMaxVitality, Character.Controlled);

        return applied == AfflictionApplyResult.Applied ? AfflictionOutcome.Applied : AfflictionOutcome.Blocked;
    }

    private static AfflictionOutcome RemoveLocally(Character character, AfflictionPrefab prefab, int? limbHealthIndex)
    {
        AfflictionApplier.Remove(character.CharacterHealth, prefab, LimbIndexes(limbHealthIndex));

        return AfflictionOutcome.Removed;
    }

    private static int[] LimbIndexes(int? limbHealthIndex) => limbHealthIndex is { } index ? [index] : [];

    private static AfflictionOutcome Send(
        Character character,
        AfflictionPrefab prefab,
        float strength,
        int[] limbs,
        AfflictionMode mode,
        bool multiplyByMaxVitality)
    {
        if (!ClientRequestPolicy.CanRequest) { return AfflictionOutcome.Denied; }

        return TrySend(character, prefab, strength, limbs, mode, multiplyByMaxVitality)
            ? AfflictionOutcome.Sent
            : AfflictionOutcome.NotSent;
    }

    private static bool TrySend(
        Character character,
        AfflictionPrefab prefab,
        float strength,
        int[] limbs,
        AfflictionMode mode,
        bool multiplyByMaxVitality)
    {
        uint requestId = ++_nextRequestId;

        AfflictionRequest request = new(
            prefab.UintIdentifier,
            strength,
            limbs,
            Protocol.Version,
            (byte)mode,
            character.ID,
            requestId,
            multiplyByMaxVitality,
            ModCompatibility.Version,
            ModCompatibility.Hash);

        try
        {
            Services.GameNetwork.Send(NetworkHeaders.AfflictionRequest, request);
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Sending the affliction request failed: {e}");
            return false;
        }

        _waitingFor = requestId;
        _sentAt = Timing.TotalTime;

        return true;
    }

    private static int[] LimbsOf(AfflictionTargets targets)
        => targets.WholeBody ? [] : [.. targets.Limbs.Select(limb => limb.HealthIndex)];
}
