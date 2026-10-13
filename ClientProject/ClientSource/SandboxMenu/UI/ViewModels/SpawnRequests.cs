namespace SandboxMenu.UI.ViewModels;

internal sealed class SpawnRequests(SpawnPanelViewModel menu, SpawnSet set, ISpawnGateway gateway)
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

        menu.Report(Describe(gateway.IntoInventory(entries, character)));
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

        menu.Host.PickWorldPosition(
            worldPosition => menu.Report(Describe(gateway.ToWorld(entries, worldPosition, Character.Controlled))));
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

    private LocalizedString Describe(SpawnRefusal? refusal) => refusal switch
    {
        SpawnRefusal.TooLarge => TextManager.Get("sandboxmenu.status.spawntoolarge"),
        SpawnRefusal.NotSent => TextManager.Get("sandboxmenu.status.sendfailed"),
        _ => TextManager.Get(gateway.IsRemote ? "sandboxmenu.status.senttoserver" : "sandboxmenu.status.spawnqueued")
    };
}
