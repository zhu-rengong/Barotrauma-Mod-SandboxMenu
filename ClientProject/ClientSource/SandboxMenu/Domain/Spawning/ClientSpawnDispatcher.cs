namespace SandboxMenu.Domain.Spawning;

// Why a spawn never left this client. Kept apart from SpawnStatus, which is the server's answer: a given spawn
// only ever produces one of the two.
internal enum SpawnRefusal
{
    TooLarge,
    NotSent
}

// Where a spawn leaves the client. In a session the items have to be made by the server — the client's own
// Entity.Spawner drops the request on the floor, so spawning locally there would report success for nothing — and
// the entries travel there as one compressed XML payload. The answer comes back as a notice the menu picks up on
// its next frame; single player keeps spawning locally, exactly as before.
internal static class ClientSpawnDispatcher
{
    private static uint _nextRequestId;
    private static uint _waitingFor;
    private static SpawnStatus? _result;
    private static int _resultQueued;
    private static int _resultProblems;

    static ClientSpawnDispatcher() => StaticState.Register(() =>
    {
        _nextRequestId = 0;
        Forget();
    });

    internal static bool IsMultiplayerClient => GameMain.NetworkMember is { IsClient: true };

    // Null means the request is on its way. Anything else is why it never left, for the status line to word.
    internal static SpawnRefusal? TrySendIntoInventory(IReadOnlyList<SpawnEntry> entries)
        => TrySend(entries, SpawnTargetKind.Inventory, Vector2.Zero);

    internal static SpawnRefusal? TrySendToWorld(IReadOnlyList<SpawnEntry> entries, Vector2 worldPosition)
        => TrySend(entries, SpawnTargetKind.World, worldPosition);

    // The game calls this from the network read: the answer is only parked here, because that is no place to
    // touch the menu (see SandboxMenuWindow.HandleNotices).
    internal static void OnResponse(SpawnResponse response)
    {
        // A late answer to a request the player has already moved on from says nothing about the newest one — and
        // with nothing in flight, an id that happens to match the sentinel is not an answer either.
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        foreach (string problem in response.Problems ?? []) { Log.Warn(problem); }

        _result = response.Status;
        _resultQueued = response.Queued;
        _resultProblems = response.Problems?.Length ?? 0;
    }

    // problems counts what the server could not do exactly as asked — a property it had to keep to itself, an entry
    // it could not spawn — which the status line mentions so the player knows to look at the log.
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
        // This attempt owns the status line from here on: an answer that is still on its way, or one nobody has
        // picked up yet, would otherwise be shown after a request that never left.
        Forget();

        var payload = new SpawnPayload();
        CollectTemplates(payload, entries, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        payload.Entries.AddRange(entries);

        if (!SpawnPayloadCodec.TryEncode(payload, out byte[] compressed)) { return SpawnRefusal.TooLarge; }

        uint requestId = ++_nextRequestId;
        var request = new SpawnRequest(
            compressed,
            target,
            worldPosition,
            (ushort)(Character.Controlled?.ID ?? 0),
            requestId);

        try
        {
            Plugin.NetworkService.Send(SandboxNetworkHeaders.SpawnRequest, request);
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

    // References travel as the presets they point at: the server has no preset folder of its own, and expanding
    // them here would duplicate what SpawnExecutor already does, cycle detection included.
    private static void CollectTemplates(SpawnPayload payload, IReadOnlyList<SpawnEntry> entries, HashSet<string> seen)
    {
        foreach (SpawnEntry entry in entries)
        {
            switch (entry)
            {
                case ItemEntry item:
                    CollectTemplates(payload, item.Inventory, seen);
                    break;

                case RefEntry reference:
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
