namespace SandboxMenu.UI.ViewModels;

// The give and spawn commands: hand the entries to the character's inventory or drop them where the player picks,
// on the client of a multiplayer game by asking the host. Outcomes land on the menu's status line.
internal sealed class SpawnRequests(SpawnMenuViewModel menu, SpawnSet set)
{
    internal void Give() => Give(set.Entries);

    internal void AtCursor() => AtCursor(set.Entries);

    internal void Give(IReadOnlyList<SpawnEntry> entries)
    {
        if (Character.Controlled is not { } character)
        {
            menu.Report(TextManager.Get("sandboxmenu.status.nocharacter"));
            return;
        }

        if (entries.Count == 0)
        {
            menu.Report(TextManager.Get("sandboxmenu.status.empty"));
            return;
        }

        if (ClientSpawnDispatcher.IsMultiplayerClient)
        {
            menu.Report(ReportSend(ClientSpawnDispatcher.TrySendIntoInventory(entries)));
            return;
        }

        ItemSpawnService.SpawnIntoInventory(entries, character, ResolveTemplate);
        menu.Report(TextManager.Get("sandboxmenu.status.spawnqueued"));
    }

    internal void AtCursor(IReadOnlyList<SpawnEntry> entries)
    {
        if (Character.Controlled is null)
        {
            menu.Report(TextManager.Get("sandboxmenu.status.nocharacter"));
            return;
        }

        if (entries.Count == 0)
        {
            menu.Report(TextManager.Get("sandboxmenu.status.empty"));
            return;
        }

        menu.Host.PickWorldPosition(worldPosition =>
        {
            if (ClientSpawnDispatcher.IsMultiplayerClient)
            {
                menu.Report(ReportSend(ClientSpawnDispatcher.TrySendToWorld(entries, worldPosition)));
                return;
            }

            ItemSpawnService.SpawnAtWorld(entries, worldPosition, Character.Controlled, ResolveTemplate);
            menu.Report(TextManager.Get("sandboxmenu.status.spawnqueued"));
        });
    }

    internal void ApplyResult(SpawnStatus status, int queued, int problems)
    {
        LocalizedString word = status switch
        {
            SpawnStatus.Ok => TextManager.GetWithVariable("sandboxmenu.status.serverok", "[count]", queued.ToString()),
            SpawnStatus.Denied => TextManager.Get("sandboxmenu.status.serverdenied"),
            SpawnStatus.BadPayload => TextManager.Get("sandboxmenu.status.serverbadpayload"),
            SpawnStatus.NoCharacter => TextManager.Get("sandboxmenu.status.servernocharacter"),
            SpawnStatus.NoSpawner => TextManager.Get("sandboxmenu.status.servernospawner"),
            SpawnStatus.Throttled => TextManager.Get("sandboxmenu.status.serverthrottled"),
            SpawnStatus.TooLarge => TextManager.Get("sandboxmenu.status.servertoolarge"),
            _ => TextManager.Get("sandboxmenu.status.serverfailed")
        };

        menu.Report(problems == 0
            ? word
            : word + " " + TextManager.GetWithVariable("sandboxmenu.status.serverproblems", "[count]", problems.ToString()));
    }

    private static LocalizedString ReportSend(SpawnRefusal? refusal) => refusal switch
    {
        SpawnRefusal.TooLarge => TextManager.Get("sandboxmenu.status.spawntoolarge"),
        SpawnRefusal.NotSent => TextManager.Get("sandboxmenu.status.sendfailed"),
        _ => TextManager.Get("sandboxmenu.status.senttoserver")
    };

    private static SpawnSet? ResolveTemplate(string name)
        => TemplateStore.TryLoad(name, out SpawnSet? set) ? set : null;
}
