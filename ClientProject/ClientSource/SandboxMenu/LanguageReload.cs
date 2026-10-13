using SandboxMenu.UI.Framework;

namespace SandboxMenu;

internal static class LanguageReload
{
    private static readonly RebuildSignal _signal = new();

    private static int _version = TextManager.LanguageVersion;

    internal static void Poll()
    {
        int version = TextManager.LanguageVersion;

        if (version == _version) { return; }

        _version = version;
        _signal.Signal();
    }

    internal static bool TakeRebuild() => _signal.Take();
}
