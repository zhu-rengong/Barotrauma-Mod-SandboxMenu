using Microsoft.Xna.Framework.Graphics;
using UiFramework.Styling;

namespace UiFramework;

// One loaded view: the tree it built, the text it was given, and the building scopes that own everything hanging
// off it.
internal sealed class ViewContext : IDisposable
{
    private readonly List<ViewScope> _scopes = [];
    private readonly List<ViewScope> _building;
    private readonly List<Action> _queuedOnce = [];
    private readonly List<Action> _firingOnce = [];
    private readonly List<Action> _focusOnOpen = [];

    private Action<string>? _diagnosticSink;

    internal ViewContext(string view)
    {
        View = view;
        Diagnostics = new MarkupDiagnostics(view);

        ViewScope root = new(_scopes);
        _scopes.Add(root);
        _building = [root];
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

    private ViewScope? Current => _building.Count > 0 ? _building[^1] : null;

    internal void EveryFrame(Action action) => Current?.EveryFrame(action);

    internal void Once(Action action) => _queuedOnce.Add(action);

    internal void FocusOnOpen(Action focus) => _focusOnOpen.Add(focus);

    // A focus asked for while the view is up waits for the click in flight to end: a text box clears itself when
    // the click lands somewhere else and would take the keyboard right back.
    internal void FocusAfterClick(Action focus) => FocusOnClickEnd(focus);

    internal void RequestFocus()
    {
        for (int i = 0; i < _focusOnOpen.Count; i++) { FocusOnClickEnd(_focusOnOpen[i]); }
    }

    private void FocusOnClickEnd(Action focus) => Once(() =>
    {
        if (PlayerInput.PrimaryMouseButtonClicked() || PlayerInput.SecondaryMouseButtonClicked())
        {
            FocusOnClickEnd(focus);
            return;
        }

        Guard.Run(focus);
    });

    internal void RunFrameActions()
    {
        for (int i = 0; i < _scopes.Count; i++) { _scopes[i].RunFrameActions(); }

        if (_queuedOnce.Count == 0) { return; }

        _firingOnce.AddRange(_queuedOnce);
        _queuedOnce.Clear();

        try
        {
            for (int i = 0; i < _firingOnce.Count; i++) { Guard.Run(_firingOnce[i]); }
        }
        finally
        {
            _firingOnce.Clear();
        }
    }

    internal void Own(IDisposable disposable)
    {
        if (Current is not { } scope)
        {
            Guard.Run(disposable.Dispose);
            return;
        }

        scope.Own(disposable);
    }

    internal ViewScope BeginScope()
    {
        ViewScope scope = new(_scopes);
        _scopes.Add(scope);
        _building.Add(scope);
        return scope;
    }

    internal void EndScope(ViewScope scope) => _building.Remove(scope);

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
        if (Root is { } root) { Guard.Run(root.Control, Detach); }

        ViewScope[] scopes = [.. _scopes];
        _scopes.Clear();
        _building.Clear();

        foreach (ViewScope scope in scopes)
        {
            Guard.Run(scope.Dispose);
        }

        _queuedOnce.Clear();
        _firingOnce.Clear();
        _focusOnOpen.Clear();
        Overlays.Clear();
        Root = null;
    }

    private static void Detach(GUIComponent control)
    {
        control.RemoveFromGUIUpdateList();
        control.RectTransform.Parent = null;
    }
}
