namespace SandboxMenu.UI.ViewModels;

internal enum StatusKind
{
    None,
    Success,
    Failure,
    Warning
}

internal static class StatusTones
{
    private static readonly Dictionary<string, StatusKind> Tones = new(StringComparer.Ordinal)
    {
        ["sandboxmenu.status.nocharacter"] = StatusKind.Warning,
        ["sandboxmenu.status.empty"] = StatusKind.Warning,
        ["sandboxmenu.status.spawnqueued"] = StatusKind.Success,
        ["sandboxmenu.status.presetsaved"] = StatusKind.Success,
        ["sandboxmenu.status.cleared"] = StatusKind.Success,
        ["sandboxmenu.status.screenshot"] = StatusKind.Success,
        ["sandboxmenu.status.screenshotfailed"] = StatusKind.Failure,
        ["sandboxmenu.status.screenshotclosed"] = StatusKind.Warning,
        ["sandboxmenu.status.presetloaded"] = StatusKind.Success,
        ["sandboxmenu.status.presetreloaded"] = StatusKind.Success,
        ["sandboxmenu.status.presetloadfailed"] = StatusKind.Failure,
        ["sandboxmenu.status.presetsavefailed"] = StatusKind.Failure,
        ["sandboxmenu.status.templatecycle"] = StatusKind.Warning,
        ["sandboxmenu.status.presetnotfound"] = StatusKind.Warning,
        ["sandboxmenu.status.presetopened"] = StatusKind.Success,
        ["sandboxmenu.status.presetopenfailed"] = StatusKind.Failure,
        ["sandboxmenu.status.nopresetname"] = StatusKind.Warning,
        ["sandboxmenu.status.nopresets"] = StatusKind.Warning,
        ["sandboxmenu.status.senttoserver"] = StatusKind.Success,
        ["sandboxmenu.status.serverok"] = StatusKind.Success,
        ["sandboxmenu.status.serverdenied"] = StatusKind.Failure,
        ["sandboxmenu.status.serverbadpayload"] = StatusKind.Failure,
        ["sandboxmenu.status.servernocharacter"] = StatusKind.Failure,
        ["sandboxmenu.status.servernospawner"] = StatusKind.Failure,
        ["sandboxmenu.status.serverthrottled"] = StatusKind.Warning,
        ["sandboxmenu.status.servertoolarge"] = StatusKind.Failure,
        ["sandboxmenu.status.serverfailed"] = StatusKind.Failure,
        ["sandboxmenu.status.spawntoolarge"] = StatusKind.Failure,
        ["sandboxmenu.status.sendfailed"] = StatusKind.Failure,
        ["sandboxmenu.status.serverproblems"] = StatusKind.Warning,
        ["sandboxmenu.afflictions.status.applied"] = StatusKind.Success,
        ["sandboxmenu.afflictions.status.removed"] = StatusKind.Success,
        ["sandboxmenu.afflictions.status.blocked"] = StatusKind.Failure,
        ["sandboxmenu.afflictions.status.senttoserver"] = StatusKind.Success,
        ["sandboxmenu.afflictions.status.sendfailed"] = StatusKind.Failure,
        ["sandboxmenu.afflictions.status.serverok"] = StatusKind.Success,
        ["sandboxmenu.afflictions.status.serverdenied"] = StatusKind.Failure,
        ["sandboxmenu.afflictions.status.serverunknown"] = StatusKind.Failure,
        ["sandboxmenu.afflictions.status.servernocharacter"] = StatusKind.Failure,
        ["sandboxmenu.afflictions.status.serverthrottled"] = StatusKind.Warning
    };

    private static (string Prefix, StatusKind Kind)[]? _prefixes;
    private static int _languageVersion = -1;

    internal static StatusKind Of(LocalizedString message)
    {
        string text = message.ToString();

        if (text.Length == 0) { return StatusKind.None; }

        if (_prefixes is null || _languageVersion != TextManager.LanguageVersion)
        {
            _prefixes = Build();
            _languageVersion = TextManager.LanguageVersion;
        }

        foreach ((string prefix, StatusKind kind) in _prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal)) { return kind; }
        }

        return StatusKind.None;
    }

    private static (string Prefix, StatusKind Kind)[] Build()
    {
        List<(string Prefix, StatusKind Kind)> prefixes = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach ((string key, StatusKind kind) in Tones)
        {
            string text = TextManager.Get(key).ToString();
            int variable = text.IndexOf('[', StringComparison.Ordinal);
            string prefix = variable > 0 ? text[..variable] : text;

            if (prefix.Length == 0 || !seen.Add(prefix)) { continue; }

            prefixes.Add((prefix, kind));
        }

        prefixes.Sort(static (left, right) => right.Prefix.Length.CompareTo(left.Prefix.Length));

        return [.. prefixes];
    }
}
