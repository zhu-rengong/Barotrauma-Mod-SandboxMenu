namespace SandboxMenu.Domain.Talents;

internal enum TalentOutcome
{
    Sent,
    Applied,
    AlreadyKnown,
    NoCharacter,
    Failed,
    Denied,
    NotSent
}

internal static class ClientTalentDispatcher
{
    private const double Timeout = 3.0;

    private static uint _nextRequestId;
    private static uint _waitingFor;
    private static double _sentAt;

    internal static event Action<TalentStatus>? Settled;

    internal static bool IsRemote => GameMain.NetworkMember is { IsClient: true };

    internal static TalentOutcome Grant(Character character, TalentPrefab prefab)
    {
        if (IsRemote)
        {
            if (!ClientRequestPolicy.CanRequest) { return TalentOutcome.Denied; }

            return TrySend(character, prefab) ? TalentOutcome.Sent : TalentOutcome.NotSent;
        }

        if (character.Info is null) { return TalentOutcome.NoCharacter; }
        if (character.HasTalent(prefab.Identifier)) { return TalentOutcome.AlreadyKnown; }

        return character.GiveTalent(prefab, true) ? TalentOutcome.Applied : TalentOutcome.Failed;
    }

    internal static void Tick()
    {
        if (_waitingFor == 0 || Timing.TotalTime - _sentAt < Timeout) { return; }

        _waitingFor = 0;

        Settled?.Invoke(TalentStatus.NoResponse);
    }

    internal static void OnResponse(TalentResponse response)
    {
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        _waitingFor = 0;

        Settled?.Invoke((TalentStatus)response.Status);
    }

    private static bool TrySend(Character character, TalentPrefab prefab)
    {
        uint requestId = ++_nextRequestId;

        TalentRequest request = new(
            prefab.UintIdentifier,
            Protocol.Version,
            character.ID,
            requestId,
            ModCompatibility.Version,
            ModCompatibility.Hash);

        try
        {
            Services.GameNetwork.Send(NetworkHeaders.TalentRequest, request);
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Sending the talent request failed: {e}");
            return false;
        }

        _waitingFor = requestId;
        _sentAt = Timing.TotalTime;

        return true;
    }
}
