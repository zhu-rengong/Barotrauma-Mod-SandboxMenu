namespace SandboxMenu.Domain.Afflictions;

internal enum AfflictionRefusal
{
    NotSent
}

internal static class ClientAfflictionDispatcher
{
    private static uint _nextRequestId;
    private static uint _waitingFor;
    private static IAfflictionResultSink? _sink;

    internal static void Listen(IAfflictionResultSink sink) => _sink = sink;

    internal static void Forget(IAfflictionResultSink sink)
    {
        if (ReferenceEquals(_sink, sink)) { _sink = null; }

        _waitingFor = 0;
    }

    internal static AfflictionRefusal? TrySend(
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
            Plugin.NetworkService.Send(NetworkHeaders.AfflictionRequest, request);
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Sending the affliction request failed: {e}");
            return AfflictionRefusal.NotSent;
        }

        _waitingFor = requestId;

        return null;
    }

    internal static void OnResponse(AfflictionResponse response)
    {
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        _waitingFor = 0;

        _sink?.OnAfflictionResult((AfflictionStatus)response.Status);
    }
}
