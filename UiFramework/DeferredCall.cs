namespace UiFramework;

internal sealed class DeferredCall
{
    private bool _queued;

    internal bool IsQueued => _queued;

    internal void Queue(ViewContext view, Action run)
    {
        if (_queued) { return; }

        _queued = true;

        view.Once(() =>
        {
            _queued = false;
            run();
        });
    }
}
