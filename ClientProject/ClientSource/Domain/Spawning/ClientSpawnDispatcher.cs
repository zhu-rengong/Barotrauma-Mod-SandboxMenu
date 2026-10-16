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

    internal static bool IsRemote => GameMain.NetworkMember is { IsClient: true };

    internal static SpawnRefusal? IntoInventory(IReadOnlyList<SpawnEntry> entries, Character character)
        => IsRemote
            ? TrySend(entries, SpawnTargetKind.Inventory, Vector2.Zero, character.ID)
            : Local(entries, character);

    internal static SpawnRefusal? ToWorld(IReadOnlyList<SpawnEntry> entries, Vector2 worldPosition, Character? character)
    {
        if (IsRemote) { return TrySend(entries, SpawnTargetKind.World, worldPosition, character?.ID ?? 0); }

        ItemSpawnService.SpawnAtWorld(entries, worldPosition, character, ResolveTemplate);

        return null;
    }

    internal static void OnResponse(SpawnResponse response)
    {
        if (_waitingFor == 0 || response.RequestId != _waitingFor) { return; }

        _waitingFor = 0;

        foreach (string problem in response.Problems ?? []) { DebugConsole.AddWarning(problem); }

        MenuWindow.Current?.ViewModel.Spawn.ApplySpawnResult(
            (SpawnStatus)response.Status, response.Queued, response.Problems?.Length ?? 0);
    }

    private static SpawnRefusal? Local(IReadOnlyList<SpawnEntry> entries, Character character)
    {
        ItemSpawnService.SpawnIntoInventory(entries, character, ResolveTemplate);

        return null;
    }

    private static SpawnSet? ResolveTemplate(string name) => TemplateStore.TryLoadShared(name, out SpawnSet? set) ? set : null;

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
            Services.GameNetwork.Send(NetworkHeaders.SpawnRequest, request);
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
                    if (!TemplateStore.TryLoadShared(reference.TemplateName, out SpawnSet? template) || template is null) { break; }

                    payload.AddTemplate(template);
                    CollectTemplates(payload, template.Entries, seen);
                    break;
            }
        }
    }
}
