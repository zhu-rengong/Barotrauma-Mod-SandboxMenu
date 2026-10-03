namespace SandboxMenu.UI.Framework;

// The host reports a resolution change from its own screen code, and the menu drops what it built for the old
// screen; the rebuild itself is picked up where the menu updates, never in the host's callback.
internal static class ScreenReload
{
    private static bool _rebuild;

    static ScreenReload() => ModLifetime.Unloading += () => _rebuild = false;

    internal static void Signal() => _rebuild = true;

    internal static bool TakeRebuild()
    {
        if (!_rebuild) { return false; }

        _rebuild = false;
        return true;
    }
}
