using System.Windows.Input;
using Microsoft.Xna.Framework.Graphics;
using UiFramework.Styling;

namespace UiFramework;

// One loaded view: the tree it built, the text it was given, and the building scopes that own everything hanging
// off it.
public sealed class ViewContext : IDisposable
{
    private readonly List<ViewScope> _scopes = [];
    private readonly List<ViewScope> _building;
    private readonly List<Action> _queuedOnce = [];
    private readonly List<Action> _firingOnce = [];
    private readonly List<Action> _focusOnOpen = [];
    private readonly List<Input.KeyBinding> _inputs = [];

    private Action<string>? _diagnosticSink;
    private ICommand? _closeCommand;

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

    public NameScope Names { get; } = new();

    internal object? DataContext { get; set; }

    internal ResourceDictionary? Resources { get; set; }

    internal ViewElement? Root { get; set; }

    // What the view was built as, for a shell that has to hold on to it (its window frame, its size).
    public ViewElement? RootElement => Root;

    public Action<string>? DiagnosticSink
    {
        get => _diagnosticSink;
        set
        {
            _diagnosticSink = value;
            Diagnostics.Sink = value;
        }
    }

    public Func<bool> IsInputBlocked { get; set; } = static () => false;

    // What closing this view means, and how a region of it is turned into a drag handle that moves the window: both
    // are the shell's, and markup asks for them by name (CloseView / Drag) rather than reaching for controls after
    // the tree has been built.
    public Action? Close { get; set; }

    public Action<RectTransform, RectTransform>? MakeDraggable { get; set; }

    public ICommand CloseCommand => _closeCommand ??= new ViewCommand(() => Close?.Invoke());

    // The window this view was built in, when it starts with one: the drag handle moves that frame.
    internal Controls.WindowElement? Window { get; set; }

    internal void AttachDrag(RectTransform region)
    {
        if (Window is not { } window) { return; }

        MakeDraggable?.Invoke(region, window.Frame.RectTransform);
    }

    public Action<Action> Dispatch { get; set; } = static work => work();

    public List<Action<SpriteBatch>> Overlays { get; } = [];

    private ViewScope? Current => _building.Count > 0 ? _building[^1] : null;

    public void EveryFrame(Action action) => Current?.EveryFrame(action);

    public void Once(Action action) => _queuedOnce.Add(action);

    internal void FocusOnOpen(Action focus) => _focusOnOpen.Add(focus);

    // A focus asked for while the view is up waits for the click in flight to end: a text box clears itself when
    // the click lands somewhere else and would take the keyboard right back.
    public void FocusAfterClick(Action focus) => FocusOnClickEnd(focus);

    public void RequestFocus()
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

        UiGuard.Run(focus);
    });

    internal void RegisterInput(Input.KeyBinding binding) => _inputs.Add(binding);

    // Runs the keys the view declares: the ones whose area is in sight, and where a key is spelled twice the more
    // specific modifier wins over the plain one (the arrows walk the list; the arrows with Alt move the entry).
    public void RunInputBindings()
    {
        for (int i = 0; i < _inputs.Count; i++)
        {
            Input.KeyBinding binding = _inputs[i];

            if (!binding.Owner.Control.Visible) { continue; }
            if (binding.RequiresNoTextInput && GUI.KeyboardDispatcher.Subscriber is not null) { continue; }
            if (!binding.Held() || !binding.Pressed() || Outranked(binding)) { continue; }

            UiGuard.Run(binding.Fire);
        }
    }

    private bool Outranked(Input.KeyBinding binding)
    {
        foreach (Input.KeyBinding other in _inputs)
        {
            if (ReferenceEquals(other, binding) || other.Specificity <= binding.Specificity) { continue; }
            if (!other.Owner.Control.Visible || !other.Held() || !other.Pressed()) { continue; }
            if (other.Keys.Intersect(binding.Keys).Any()) { return true; }
        }

        return false;
    }

    public void RunFrameActions()
    {
        for (int i = 0; i < _scopes.Count; i++) { _scopes[i].RunFrameActions(); }

        if (_queuedOnce.Count == 0) { return; }

        _firingOnce.AddRange(_queuedOnce);
        _queuedOnce.Clear();

        try
        {
            for (int i = 0; i < _firingOnce.Count; i++) { UiGuard.Run(_firingOnce[i]); }
        }
        finally
        {
            _firingOnce.Clear();
        }
    }

    public void Own(IDisposable disposable)
    {
        if (Current is not { } scope)
        {
            UiGuard.Run(disposable.Dispose);
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

        return Resources?.Find(key) ?? GlobalResources.Find(key);
    }

    public void Dispose()
    {
        if (Root is { } root) { UiGuard.Run(root.Control, Detach); }

        ViewScope[] scopes = [.. _scopes];
        _scopes.Clear();
        _building.Clear();

        foreach (ViewScope scope in scopes)
        {
            UiGuard.Run(scope.Dispose);
        }

        _queuedOnce.Clear();
        _firingOnce.Clear();
        _focusOnOpen.Clear();
        Overlays.Clear();
        _inputs.Clear();
        Window = null;
        Root = null;
    }

    private static void Detach(GUIComponent control)
    {
        control.RemoveFromGUIUpdateList();
        control.RectTransform.Parent = null;
    }
}
