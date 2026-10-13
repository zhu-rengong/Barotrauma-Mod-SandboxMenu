namespace SandboxMenu.Domain.Afflictions;

internal enum AfflictionRefusal
{
    NotSent
}

internal static class ClientAfflictionDispatcher
{
    private static uint _nextRequestId;
    private static uint _waitingFor;
    private static AfflictionStatus? _result;

    internal static bool IsMultiplayerClient => GameMain.NetworkMember is { IsClient: true };

    internal static AfflictionRefusal? TrySend(
        Character character,
        AfflictionPrefab prefab,
        float strength,
        int[] limbs,
        AfflictionMode mode)
    {
        Forget();

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
            Plugin.NetworkService.Send(NetworkHeaders.AfflictionRequest, request);
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Sending the affliction request failed: {e}");
            return AfflictionRefusal.NotSent;
        }

        _waitingFor = requestId;
        _result = null;

        return null;
    }

    internal static void OnResponse(AfflictionResponse response)
    {
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        _result = (AfflictionStatus)response.Status;
    }

    internal static bool TryTakeResult(out AfflictionStatus status)
    {
        if (_result is not { } result)
        {
            status = AfflictionStatus.Ok;
            return false;
        }

        status = result;
        Forget();

        return true;
    }

    private static void Forget()
    {
        _waitingFor = 0;
        _result = null;
    }
}
