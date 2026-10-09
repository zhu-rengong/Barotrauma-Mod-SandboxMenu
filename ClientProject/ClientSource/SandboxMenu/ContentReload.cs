using SandboxMenu.UI.Framework;

namespace SandboxMenu;

internal static class ContentReload
{
    private static readonly RebuildSignal _signal = new();

    private static int _version = ContentRevision.Current;

    internal static void Poll()
    {
        int version = ContentRevision.Current;

        if (version == _version) { return; }

        _version = version;

        _signal.Signal();
    }

    internal static bool TakeRebuild() => _signal.Take();
}
