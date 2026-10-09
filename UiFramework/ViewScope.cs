namespace UiFramework;

internal sealed class ViewScope(List<ViewScope> live) : IDisposable
{
    private readonly List<IDisposable> _owned = [];
    private readonly List<Data.Binding> _bindings = [];
    private readonly List<Action> _frameActions = [];

    private bool _paused;
    private bool _disposed;

    internal void Pause() => _paused = true;

    internal void Resume() => _paused = false;

    internal void EveryFrame(Action action) => _frameActions.Add(action);

    internal void RunFrameActions()
    {
        if (_paused || _disposed) { return; }

        for (int i = 0; i < _frameActions.Count; i++)
        {
            UiGuard.Run(_frameActions[i]);
        }
    }

    internal void Own(IDisposable disposable)
    {
        _owned.Add(disposable);

        if (disposable is Data.Binding binding) { _bindings.Add(binding); }
    }

    internal void Retarget(object? source)
    {
        for (int i = 0; i < _bindings.Count; i++) { _bindings[i].Retarget(source); }
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _frameActions.Clear();
        _bindings.Clear();
        live.Remove(this);

        for (int i = 0; i < _owned.Count; i++)
        {
            UiGuard.Run(_owned[i].Dispose);
        }

        _owned.Clear();
    }
}
