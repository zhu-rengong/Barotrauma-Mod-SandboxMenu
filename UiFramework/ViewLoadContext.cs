using Microsoft.Xna.Framework.Graphics;
using UiFramework.Styling;

namespace UiFramework;

internal sealed class ViewLoadContext(string view) : IDisposable
{
    private readonly List<OwnershipScope> _scopes = [new()];

    internal string View { get; } = view;

    internal MarkupDiagnostics Diagnostics { get; } = new(view);

    internal NameScope Names { get; } = new();

    internal object? DataContext { get; set; }

    internal ResourceDictionary? Resources { get; set; }

    internal ViewElement? Root { get; set; }

    private readonly List<Action> _frameActions = [];
    private readonly List<Action> _oneShots = [];
    private readonly List<Action> _running = [];
    private readonly List<Action> _focusTargets = [];

    internal void EveryFrame(Action action) => _frameActions.Add(action);

    internal void Once(Action action) => _oneShots.Add(action);

    // A control the view opens with the keyboard already in it. The focus is handed over on a frame whose click has
    // passed: the click that opened the window takes it off a control selected while the game was still walking
    // that frame's clicks.
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
        for (int i = 0; i < _frameActions.Count; i++) { Run(_frameActions[i]); }

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

    internal List<Action> AfterRegister { get; } = [];

    private Action<string>? _diagnosticSink;

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

    internal void Own(IDisposable disposable)
    {
        if (_scopes.Count == 0)
        {
            Guard.Run($"Releasing state registered after '{View}' went away failed", disposable.Dispose);
            return;
        }

        _scopes[^1].Own(disposable);
    }

    internal OwnershipScope BeginScope()
    {
        OwnershipScope scope = new();
        _scopes.Add(scope);
        return scope;
    }

    internal void EndScope(OwnershipScope scope) => _scopes.Remove(scope);

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
        // Built into a live parent, or into the canvas when the caller gave none: either way the tree is in the
        // game's GUI, and a tree of this assembly hanging off a static root is what keeps the mod from being
        // collected after unloading. Letting a view go therefore takes it out of the GUI, not just unbinds it.
        if (Root is { } root)
        {
            Guard.Run($"Removing view '{View}' from the GUI failed", () =>
            {
                root.Control.RemoveFromGUIUpdateList();
                root.Control.RectTransform.Parent = null;
            });
        }

        OwnershipScope[] scopes = [.. _scopes];
        _scopes.Clear();

        foreach (OwnershipScope scope in scopes)
        {
            Guard.Run($"Disposing the state of '{View}' failed", scope.Dispose);
        }

        _frameActions.Clear();
        _oneShots.Clear();
        _running.Clear();
        _focusTargets.Clear();
        AfterRegister.Clear();
        Root = null;
    }
}

internal sealed class OwnershipScope : IDisposable
{
    private readonly List<IDisposable> _owned = [];

    internal void Own(IDisposable disposable) => _owned.Add(disposable);

    public void Dispose()
    {
        for (int i = 0; i < _owned.Count; i++)
        {
            Guard.Run("Letting a view's state go failed", _owned[i].Dispose);
        }

        _owned.Clear();
    }
}
