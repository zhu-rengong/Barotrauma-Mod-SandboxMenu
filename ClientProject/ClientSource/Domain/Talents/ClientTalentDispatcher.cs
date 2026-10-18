namespace SandboxMenu.Domain.Talents;

internal enum TalentOutcome
{
    Sent,
    Applied,
    AlreadyKnown,
    NoCharacter,
    Failed,
    NotSent
}

internal static class ClientTalentDispatcher
{
    private static uint _nextRequestId;
    private static uint _waitingFor;

    internal static event Action<TalentStatus>? Settled;

    internal static bool IsRemote => GameMain.NetworkMember is { IsClient: true };

    internal static TalentOutcome Grant(Character character, TalentPrefab prefab)
    {
        if (IsRemote) { return TrySend(character, prefab) ? TalentOutcome.Sent : TalentOutcome.NotSent; }

        if (character.Info is null) { return TalentOutcome.NoCharacter; }
        if (character.HasTalent(prefab.Identifier)) { return TalentOutcome.AlreadyKnown; }

        return character.GiveTalent(prefab, true) ? TalentOutcome.Applied : TalentOutcome.Failed;
    }

    internal static void OnResponse(TalentResponse response)
    {
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        _waitingFor = 0;

        Settled?.Invoke((TalentStatus)response.Status);
    }

    private static bool TrySend(Character character, TalentPrefab prefab)
    {
        _waitingFor = 0;

        uint requestId = ++_nextRequestId;

        TalentRequest request = new(prefab.UintIdentifier, Protocol.Version, character.ID, requestId);

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

        return true;
    }
}
