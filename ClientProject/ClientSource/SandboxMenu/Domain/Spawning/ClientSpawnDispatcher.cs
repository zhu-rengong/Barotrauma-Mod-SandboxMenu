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
    private static SpawnStatus? _result;
    private static int _resultQueued;
    private static int _resultProblems;

    static ClientSpawnDispatcher() => ModLifetime.Unloading += () =>
    {
        _nextRequestId = 0;
        Forget();
    };

    internal static bool IsMultiplayerClient => GameMain.NetworkMember is { IsClient: true };

    internal static SpawnRefusal? TrySendIntoInventory(IReadOnlyList<SpawnEntry> entries)
        => TrySend(entries, SpawnTargetKind.Inventory, Vector2.Zero);

    internal static SpawnRefusal? TrySendToWorld(IReadOnlyList<SpawnEntry> entries, Vector2 worldPosition)
        => TrySend(entries, SpawnTargetKind.World, worldPosition);

    internal static void OnResponse(SpawnResponse response)
    {
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        foreach (string problem in response.Problems ?? []) { Log.Warn(problem); }

        _result = (SpawnStatus)response.Status;
        _resultQueued = response.Queued;
        _resultProblems = response.Problems?.Length ?? 0;
    }

    internal static bool TryTakeResult(out SpawnStatus status, out int queued, out int problems)
    {
        if (_result is not { } result)
        {
            status = SpawnStatus.Ok;
            queued = 0;
            problems = 0;
            return false;
        }

        status = result;
        queued = _resultQueued;
        problems = _resultProblems;

        Forget();
        return true;
    }

    private static SpawnRefusal? TrySend(IReadOnlyList<SpawnEntry> entries, SpawnTargetKind target, Vector2 worldPosition)
    {
        Forget();

        SpawnPayload payload = new();
        CollectTemplates(payload, entries, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        payload.Entries.AddRange(entries);

        if (!SpawnPayloadCodec.TryEncode(payload, out byte[] compressed)) { return SpawnRefusal.TooLarge; }

        uint requestId = ++_nextRequestId;
        SpawnRequest request = new(
            compressed,
            (byte)target,
            worldPosition,
            (ushort)(Character.Controlled?.ID ?? 0),
            requestId);

        try
        {
            Plugin.NetworkService.Send(NetworkHeaders.SpawnRequest, request);
        }
        catch (Exception e)
        {
            Log.Warn("Sending the spawn request failed", e);
            return SpawnRefusal.NotSent;
        }

        _waitingFor = requestId;
        _result = null;

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

    private static void Forget()
    {
        _waitingFor = 0;
        _result = null;
        _resultQueued = 0;
        _resultProblems = 0;
    }
}
