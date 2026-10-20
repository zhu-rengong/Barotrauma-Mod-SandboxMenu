namespace SandboxMenu.Domain.Spawning;

internal enum SpawnRefusal
{
    TooLarge,
    NotSent,
    Denied
}

internal static class ClientSpawnDispatcher
{
    private const double Timeout = 3.0;

    private static uint _nextRequestId;
    private static uint _waitingFor;
    private static double _sentAt;

    internal static event Action<SpawnStatus, int, int>? Settled;

    internal static bool IsRemote => GameMain.NetworkMember is { IsClient: true };

    internal static SpawnRefusal? IntoInventory(IReadOnlyList<SpawnEntry> entries, Character character)
    {
        if (!IsRemote) { return Local(entries, character); }
        if (!ClientRequestPolicy.CanRequest) { return SpawnRefusal.Denied; }

        return TrySend(entries, SpawnTargetKind.Inventory, Vector2.Zero, character.ID);
    }

    internal static SpawnRefusal? ToWorld(IReadOnlyList<SpawnEntry> entries, Vector2 worldPosition, Character? character)
    {
        if (!IsRemote)
        {
            SpawnExecutor executor = new(new SpawnTarget.AtWorld(worldPosition, character), ResolveTemplate);
            executor.Run(entries);

            return null;
        }

        if (!ClientRequestPolicy.CanRequest) { return SpawnRefusal.Denied; }

        return TrySend(entries, SpawnTargetKind.World, worldPosition, character?.ID ?? 0);
    }

    internal static void Tick()
    {
        if (_waitingFor == 0 || Timing.TotalTime - _sentAt < Timeout) { return; }

        _waitingFor = 0;

        Settled?.Invoke(SpawnStatus.NoResponse, 0, 0);
    }

    internal static void OnResponse(SpawnResponse response)
    {
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        _waitingFor = 0;

        foreach (string problem in response.Problems ?? []) { Services.DebugConsole.AddWarning(problem); }

        Settled?.Invoke((SpawnStatus)response.Status, response.Queued, response.Problems?.Length ?? 0);
    }

    private static SpawnRefusal? Local(IReadOnlyList<SpawnEntry> entries, Character character)
    {
        if (character.Inventory is { } inventory)
        {
            SpawnExecutor executor = new(new SpawnTarget.IntoInventory(inventory, character), ResolveTemplate);
            executor.Run(entries);
        }

        return null;
    }

    private static SpawnSet? ResolveTemplate(string name) => TemplateStore.TryLoadShared(name, out SpawnSet? set) ? set : null;

    private static SpawnRefusal? TrySend(
        IReadOnlyList<SpawnEntry> entries,
        SpawnTargetKind target,
        Vector2 worldPosition,
        ushort characterId)
    {
        SpawnMessage payload = new();
        CollectTemplates(payload, entries, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        payload.Entries.AddRange(entries);

        if (!SpawnMessageCodec.TryEncode(payload, out byte[] compressed)) { return SpawnRefusal.TooLarge; }

        uint requestId = ++_nextRequestId;
        SpawnRequest request = new(
            compressed,
            Protocol.Version,
            (byte)target,
            worldPosition,
            characterId,
            requestId,
            ModCompatibility.Version,
            ModCompatibility.Hash);

        try
        {
            Services.GameNetwork.Send(NetworkHeaders.SpawnRequest, request);
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Sending the spawn request failed: {e}");
            return SpawnRefusal.NotSent;
        }

        _waitingFor = requestId;
        _sentAt = Timing.TotalTime;

        return null;
    }

    private static void CollectTemplates(SpawnMessage payload, IReadOnlyList<SpawnEntry> entries, HashSet<string> seen)
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
                    if (!TemplateStore.TryLoadShared(reference.TemplateName, out SpawnSet? template) || template is null) { break; }

                    payload.AddTemplate(template);
                    CollectTemplates(payload, template.Entries, seen);
                    break;
            }
        }
    }
}
