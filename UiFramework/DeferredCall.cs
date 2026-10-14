namespace UiFramework;

internal sealed class DeferredCall
{
    private readonly Action _fire;
    private bool _queued;

    internal DeferredCall(Action run) => _fire = () =>
    {
        _queued = false;
        run();
    };

    internal bool IsQueued => _queued;

    internal void Queue(ViewContext view)
    {
        if (_queued) { return; }

        _queued = true;
        view.Once(_fire);
    }
}
