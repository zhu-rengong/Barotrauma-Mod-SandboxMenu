namespace SandboxMenu.Domain.Spawning;

internal enum SpawnRefusal
{
    TooLarge,
    NotSent
}

internal static class ClientSpawnDispatcher
{
    private static uint _nextRequestId;
    private static uint _waitingFor;
    private static ISpawnResultSink? _sink;

    internal static void Listen(ISpawnResultSink sink) => _sink = sink;

    internal static void Forget(ISpawnResultSink sink)
    {
        if (ReferenceEquals(_sink, sink)) { _sink = null; }

        _waitingFor = 0;
    }

    internal static SpawnRefusal? TrySendIntoInventory(IReadOnlyList<SpawnEntry> entries, ushort characterId)
        => TrySend(entries, SpawnTargetKind.Inventory, Vector2.Zero, characterId);

    internal static SpawnRefusal? TrySendToWorld(IReadOnlyList<SpawnEntry> entries, Vector2 worldPosition, ushort characterId)
        => TrySend(entries, SpawnTargetKind.World, worldPosition, characterId);

    internal static void OnResponse(SpawnResponse response)
    {
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        _waitingFor = 0;

        foreach (string problem in response.Problems ?? []) { DebugConsole.AddWarning(problem); }

        _sink?.OnSpawnResult((SpawnStatus)response.Status, response.Queued, response.Problems?.Length ?? 0);
    }

    private static SpawnRefusal? TrySend(
        IReadOnlyList<SpawnEntry> entries,
        SpawnTargetKind target,
        Vector2 worldPosition,
        ushort characterId)
    {
        _waitingFor = 0;

        SpawnPayload payload = new();
        CollectTemplates(payload, entries, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        payload.Entries.AddRange(entries);

        if (!SpawnPayloadCodec.TryEncode(payload, out byte[] compressed)) { return SpawnRefusal.TooLarge; }

        uint requestId = ++_nextRequestId;
        SpawnRequest request = new(
            compressed,
            (byte)target,
            worldPosition,
            characterId,
            requestId);

        try
        {
            Plugin.NetworkService.Send(NetworkHeaders.SpawnRequest, request);
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Sending the spawn request failed: {e}");
            return SpawnRefusal.NotSent;
        }

        _waitingFor = requestId;

        return null;
    }

    private static void CollectTemplates(SpawnPayload payload, IReadOnlyList<SpawnEntry> entries, HashSet<string> seen)
    {
        foreach (SpawnEntry entry in entries)
        {
            switch (entry)
            {
                case ItemEntry item:
                    CollectTemplates(payload, item.Inventory, seen);
                    break;

                case ReferenceEntry reference:
                    if (string.IsNullOrEmpty(reference.TemplateName) || !seen.Add(reference.TemplateName)) { break; }
                    if (!TemplateStore.TryLoad(reference.TemplateName, out SpawnSet? template) || template is null) { break; }

                    payload.AddTemplate(template);
                    CollectTemplates(payload, template.Entries, seen);
                    break;
            }
        }
    }
}
