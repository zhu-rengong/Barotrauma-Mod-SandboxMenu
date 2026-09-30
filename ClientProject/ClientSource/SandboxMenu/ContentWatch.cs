namespace SandboxMenu;

// The game reloads the text packs together with the content packages, which is what the revision counts. Everything
// the mod cached from the old packages has to go right away, even while the menu is closed: rows and catalogs left
// behind would keep the plugin of a package that is being unloaded referenced, and the game then reports that the
// package could not be fully unloaded. Rebuilding the views stays with the menu, which is what TakeChanged is for.
internal static class ContentWatch
{
    private static readonly List<Action> _clearing = [];

    private static int _version = ContentRevision.Current;
    private static bool _changed;

    static ContentWatch() => StaticState.Register(() =>
    {
        _version = ContentRevision.Current;
        _changed = false;
        _clearing.Clear();
    });

    // Anything that keeps something derived from the content packages (prefabs, their components, their package)
    // registers its clearing here, the same way as with StaticState: it has to go the moment the packages change.
    internal static void Register(Action clear) => _clearing.Add(clear);

    internal static void Watch()
    {
        int version = ContentRevision.Current;

        if (version == _version) { return; }

        _version = version;

        if (_changed) { return; }

        _changed = true;

        for (int i = 0; i < _clearing.Count; i++)
        {
            Guard.Run("Clearing the mod's content data failed", _clearing[i]);
        }

        SandboxMenuWindow.ReleaseContent();
    }

    internal static bool TakeChanged()
    {
        if (!_changed) { return false; }

        _changed = false;
        return true;
    }
}
