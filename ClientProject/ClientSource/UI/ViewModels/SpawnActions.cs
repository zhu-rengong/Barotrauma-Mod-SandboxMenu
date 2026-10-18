namespace SandboxMenu.UI.ViewModels;

internal sealed class SpawnActions(
    Action<StatusKind, LocalizedString> report,
    IDialogHost host,
    SpawnSet set)
{
    internal void Give() => Give(set.Entries);

    internal void AtCursor() => AtCursor(set.Entries);

    internal void Give(IReadOnlyList<SpawnEntry> entries)
    {
        if (Character.Controlled is not { } character)
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.nocharacter"));
            return;
        }

        if (entries.Count == 0)
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.empty"));
            return;
        }

        SpawnRefusal? refusal = ClientSpawnDispatcher.IntoInventory(entries, character);

        report(KindOf(refusal), Describe(refusal));
    }

    internal void AtCursor(IReadOnlyList<SpawnEntry> entries)
    {
        if (Character.Controlled is null)
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.nocharacter"));
            return;
        }

        if (entries.Count == 0)
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.empty"));
            return;
        }

        host.PickWorldPosition(worldPosition =>
        {
            SpawnRefusal? refusal = ClientSpawnDispatcher.ToWorld(entries, worldPosition, Character.Controlled);

            report(KindOf(refusal), Describe(refusal));
        });
    }

    internal void ApplyResult(SpawnStatus status, int queued, int problems)
    {
        (StatusKind kind, LocalizedString word) = status switch
        {
            SpawnStatus.Ok => (StatusKind.Success, TextManager.GetWithVariable("sandboxmenu.status.serverok", "[count]", queued.ToString())),
            SpawnStatus.Denied => (StatusKind.Failure, TextManager.Get("sandboxmenu.status.serverdenied")),
            SpawnStatus.BadPayload => (StatusKind.Failure, TextManager.Get("sandboxmenu.status.serverbadpayload")),
            SpawnStatus.NoCharacter => (StatusKind.Failure, TextManager.Get("sandboxmenu.status.servernocharacter")),
            SpawnStatus.NoSpawner => (StatusKind.Failure, TextManager.Get("sandboxmenu.status.servernospawner")),
            SpawnStatus.Throttled => (StatusKind.Warning, TextManager.Get("sandboxmenu.status.serverthrottled")),
            SpawnStatus.TooLarge => (StatusKind.Failure, TextManager.Get("sandboxmenu.status.servertoolarge")),
            _ => (StatusKind.Failure, TextManager.Get("sandboxmenu.status.serverfailed"))
        };

        report(
            problems == 0 ? kind : StatusKind.Warning,
            problems == 0
                ? word
                : word + " " + TextManager.GetWithVariable("sandboxmenu.status.serverproblems", "[count]", problems.ToString()));
    }

    private static StatusKind KindOf(SpawnRefusal? refusal) => refusal is null ? StatusKind.Success : StatusKind.Failure;

    private static LocalizedString Describe(SpawnRefusal? refusal) => refusal switch
    {
        SpawnRefusal.TooLarge => TextManager.Get("sandboxmenu.status.spawntoolarge"),
        SpawnRefusal.NotSent => TextManager.Get("sandboxmenu.status.sendfailed"),
        _ => TextManager.Get(ClientSpawnDispatcher.IsRemote ? "sandboxmenu.status.senttoserver" : "sandboxmenu.status.spawnqueued")
    };
}
