using System.Windows.Input;
using Microsoft.Xna.Framework.Graphics;
using UiFramework.Styling;

namespace UiFramework;

public sealed class NameScope
{
    private readonly Dictionary<string, ViewElement> _elements = new(StringComparer.Ordinal);

    internal void Add(string name, ViewElement element, MarkupDiagnostics diagnostics, MarkupNode node)
    {
        if (_elements.TryAdd(name, element)) { return; }

        diagnostics.Report($"the name '{name}' is used twice", node);
    }

    public ViewElement? Find(string name) => _elements.GetValueOrDefault(name);
}

internal sealed class ViewCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}

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

    public Action? Close { get; set; }

    public Action<RectTransform, RectTransform>? MakeDraggable { get; set; }

    public ICommand CloseCommand => _closeCommand ??= new ViewCommand(() => Close?.Invoke());

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

    public void FocusAfterClick(Action focus) => FocusOnClickEnd(focus);

    public void RequestFocus()
    {
        for (int i = 0; i < _focusOnOpen.Count; i++) { FocusOnClickEnd(_focusOnOpen[i]); }
    }

    internal void RegisterInput(Input.KeyBinding binding) => _inputs.Add(binding);

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

    private void FocusOnClickEnd(Action focus) => Once(() =>
    {
        if (PlayerInput.PrimaryMouseButtonClicked() || PlayerInput.SecondaryMouseButtonClicked())
        {
            FocusOnClickEnd(focus);
            return;
        }

        UiGuard.Run(focus);
    });

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

    private static void Detach(GUIComponent control)
    {
        control.RemoveFromGUIUpdateList();
        control.RectTransform.Parent = null;
    }
}
