namespace UiFramework;

// Every static holder of the framework's state gives that state up here, and what is registered after Unload never
// runs — the host reports an incomplete unload for anything left behind. The mod drives this from its own teardown.
internal static class UiLifetime
{
    private static Action? _unloading;
    private static bool _unloaded;

    internal static event Action? Unloading
    {
        add
        {
            if (_unloaded) { return; }

            _unloading += value;
        }
        remove => _unloading -= value;
    }

    internal static void Unload()
    {
        _unloaded = true;

        if (_unloading is not { } unloading) { return; }

        _unloading = null;

        UiGuard.RunEach(unloading);
    }
}
