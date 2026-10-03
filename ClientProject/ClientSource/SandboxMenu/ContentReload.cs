namespace SandboxMenu;

// The text packs reload with the content packages, and that revision is the only signal the mod gets from it:
// anything derived from the old packages lets go of it here, even while the menu is closed.
internal static class ContentReload
{
    private static int _version = ContentRevision.Current;
    private static bool _rebuild;

    static ContentReload() => ModLifetime.Unloading += Reset;

    internal static event Action? Invalidated;

    internal static void Poll()
    {
        int version = ContentRevision.Current;

        if (version == _version) { return; }

        _version = version;

        if (_rebuild) { return; }

        _rebuild = true;

        Guard.RunEach(Invalidated);
    }

    internal static bool TakeRebuild()
    {
        if (!_rebuild) { return false; }

        _rebuild = false;
        return true;
    }

    private static void Reset()
    {
        _version = ContentRevision.Current;
        _rebuild = false;
        Invalidated = null;
    }
}
