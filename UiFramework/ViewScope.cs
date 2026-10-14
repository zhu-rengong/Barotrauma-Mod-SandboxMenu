using UiFramework.Data;

namespace UiFramework;

internal sealed class ViewScope(List<ViewScope> live) : IDisposable, IPauseable
{
    private readonly List<IDisposable> _owned = [];
    private readonly List<Binding> _bindings = [];
    private readonly List<Action> _frameActions = [];
    private readonly List<Func<bool>> _untilActions = [];

    private bool _paused;
    private bool _disposed;

    public bool IsPaused => _paused;

    internal void Pause() => _paused = true;

    internal void Resume() => _paused = false;

    internal void EveryFrame(Action action) => _frameActions.Add(action);

    internal void EveryFrame(Func<bool> action) => _untilActions.Add(action);

    internal bool Remove(Action action) => _frameActions.Remove(action);

    internal bool Remove(Func<bool> action) => _untilActions.Remove(action);

    internal void RunFrameActions()
    {
        if (_paused || _disposed) { return; }

        for (int i = 0; i < _frameActions.Count; i++)
        {
            UiGuard.Run(_frameActions[i]);
        }

        for (int i = _untilActions.Count - 1; i >= 0; i--)
        {
            if (UiGuard.TryRun(_untilActions[i], static action => action())) { _untilActions.RemoveAt(i); }
        }
    }

    internal void Own(IDisposable disposable)
    {
        _owned.Add(disposable);

        if (disposable is Binding binding) { _bindings.Add(binding); }
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
        _untilActions.Clear();
        _bindings.Clear();
        live.Remove(this);

        for (int i = 0; i < _owned.Count; i++)
        {
            UiGuard.Run(_owned[i].Dispose);
        }

        _owned.Clear();
    }
}
