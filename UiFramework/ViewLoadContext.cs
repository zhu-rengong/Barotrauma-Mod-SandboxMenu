using Microsoft.Xna.Framework.Graphics;
using UiFramework.Styling;

namespace UiFramework;

internal sealed class ViewLoadContext : IDisposable
{
    private readonly List<OwnershipScope> _live = [];
    private readonly List<OwnershipScope> _open;
    private readonly List<Action> _oneShots = [];
    private readonly List<Action> _running = [];
    private readonly List<Action> _focusTargets = [];

    private Action<string>? _diagnosticSink;

    internal ViewLoadContext(string view)
    {
        View = view;
        Diagnostics = new MarkupDiagnostics(view);

        OwnershipScope root = new(_live, view);
        _live.Add(root);
        _open = [root];
    }

    internal string View { get; }

    internal MarkupDiagnostics Diagnostics { get; }

    internal NameScope Names { get; } = new();

    internal object? DataContext { get; set; }

    internal ResourceDictionary? Resources { get; set; }

    internal ViewElement? Root { get; set; }

    internal Action<string>? DiagnosticSink
    {
        get => _diagnosticSink;
        set
        {
            _diagnosticSink = value;
            Diagnostics.Sink = value;
        }
    }

    internal Func<bool> IsInputBlocked { get; set; } = static () => false;

    internal Action<Action> Dispatch { get; set; } = static work => work();

    internal List<Action<SpriteBatch>> Overlays { get; } = [];

    private OwnershipScope? Current => _open.Count > 0 ? _open[^1] : null;

    internal void EveryFrame(Action action) => Current?.EveryFrame(action);

    internal void Once(Action action) => _oneShots.Add(action);

    internal void FocusOnOpen(Action focus) => _focusTargets.Add(focus);

    internal void RequestFocus()
    {
        for (int i = 0; i < _focusTargets.Count; i++) { FocusOnClickEnd(_focusTargets[i]); }
    }

    private void FocusOnClickEnd(Action focus) => Once(() =>
    {
        if (PlayerInput.PrimaryMouseButtonClicked() || PlayerInput.SecondaryMouseButtonClicked())
        {
            FocusOnClickEnd(focus);
            return;
        }

        Run(focus);
    });

    internal void RunFrameActions()
    {
        for (int i = 0; i < _live.Count; i++) { _live[i].RunFrameActions(); }

        if (_oneShots.Count == 0) { return; }

        _running.AddRange(_oneShots);
        _oneShots.Clear();

        try
        {
            for (int i = 0; i < _running.Count; i++) { Run(_running[i]); }
        }
        finally
        {
            _running.Clear();
        }
    }

    private void Run(Action action) => Guard.Run($"A frame action of '{View}' failed", action);

    internal void Own(IDisposable disposable)
    {
        if (Current is not { } scope)
        {
            Guard.Run($"Releasing state registered after '{View}' went away failed", disposable.Dispose);
            return;
        }

        scope.Own(disposable);
    }

    internal OwnershipScope BeginScope()
    {
        OwnershipScope scope = new(_live, View);
        _live.Add(scope);
        _open.Add(scope);
        return scope;
    }

    internal void EndScope(OwnershipScope scope) => _open.Remove(scope);

    internal object? FindResource(string key, ViewElement? element)
    {
        for (ViewElement? current = element; current is not null; current = current.Parent)
        {
            if (current.Resources?.Find(key) is { } found) { return found; }
        }

        return Resources?.Find(key) ?? DefaultTheme.Theme.Find(key);
    }

    public void Dispose()
    {
        if (Root is { } root)
        {
            Guard.Run($"Removing view '{View}' from the GUI failed", () =>
            {
                root.Control.RemoveFromGUIUpdateList();
                root.Control.RectTransform.Parent = null;
            });
        }

        OwnershipScope[] scopes = [.. _live];
        _live.Clear();
        _open.Clear();

        foreach (OwnershipScope scope in scopes)
        {
            Guard.Run($"Disposing the state of '{View}' failed", scope.Dispose);
        }

        _oneShots.Clear();
        _running.Clear();
        _focusTargets.Clear();
        Overlays.Clear();
        Root = null;
    }
}

internal sealed class OwnershipScope(List<OwnershipScope> live, string view) : IDisposable
{
    private readonly List<IDisposable> _owned = [];
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
            Guard.Run($"A frame action of '{view}' failed", _frameActions[i]);
        }
    }

    internal void Own(IDisposable disposable) => _owned.Add(disposable);

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _frameActions.Clear();
        live.Remove(this);

        for (int i = 0; i < _owned.Count; i++)
        {
            Guard.Run("Letting a view's state go failed", _owned[i].Dispose);
        }

        _owned.Clear();
    }
}
