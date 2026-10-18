using System.Windows.Input;
using Microsoft.Xna.Framework.Graphics;
using UiFramework.Animation;
using UiFramework.Layout;
using KeyCode = Microsoft.Xna.Framework.Input.Keys;

namespace UiFramework;

internal sealed class NameScope
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
    private readonly Dictionary<KeyCode, List<Input.KeyBinding>> _byKey = [];
    private readonly HashSet<ViewElement> _stateWatchers = [];
    private readonly HashSet<ViewElement> _valueWatchers = [];
    private ViewElement[] _stateSnapshot = [];
    private ViewElement[] _valueSnapshot = [];
    private bool _stateDirty = true;
    private bool _valueDirty = true;
    private readonly List<Controls.StackElement> _batchedShares = [];
    private readonly List<Action<SpriteBatch>> _overlays = [];

    private int _layoutBatch;
    private bool _flushingShares;
    private bool _layoutRequested;

    private Action<string>? _diagnosticSink;
    private ICommand? _closeCommand;

    internal ViewContext(string view, AnimationHost animations)
    {
        View = view;
        Diagnostics = new MarkupDiagnostics(view);
        Animations = animations;

        ViewScope root = new(_scopes);
        _scopes.Add(root);
        _building = [root];
    }

    internal string View { get; }

    internal MarkupDiagnostics Diagnostics { get; }

    internal AnimationHost Animations { get; }

    internal NameScope Names { get; } = new();

    internal object? DataContext { get; set; }

    public Point? ContentSize { get; set; }

    internal ResourceDictionary? Resources { get; set; }

    internal ViewElement? Root { get; set; }

    public ViewElement? RootElement => Root;

    internal Action<string>? DiagnosticSink
    {
        get => _diagnosticSink;
        set
        {
            _diagnosticSink = value;
            Diagnostics.Sink = value;
        }
    }

    public Func<bool> IsListDragBlocked { get; set; } = static () => false;

    public Action? Close { get; set; }

    public Func<bool>? CloseTransient { get; set; }

    public Action<RectTransform, RectTransform>? MakeDraggable { get; set; }

    internal ICommand CloseCommand => _closeCommand ??= new ViewCommand(() => Close?.Invoke());

    internal Controls.WindowElement? Window { get; set; }

    internal void AttachDrag(RectTransform region)
    {
        if (Window is not { } window) { return; }

        MakeDraggable?.Invoke(region, window.Frame.RectTransform);
    }

    public Action<Action> Dispatch { get; set; } = static work => work();

    internal IReadOnlyList<Action<SpriteBatch>> Overlays => _overlays;

    public void AddOverlay(Action<SpriteBatch> draw) => _overlays.Add(draw);

    public void RemoveOverlay(Action<SpriteBatch> draw) => _overlays.Remove(draw);

    private ViewScope? Current => _building.Count > 0 ? _building[^1] : null;

    internal ViewScope? CurrentScope => Current;

    public void EveryFrame(Action action) => Current?.EveryFrame(action);

    public void EveryFrame(Func<bool> action) => Current?.EveryFrame(action);

    internal void WatchState(ViewElement element)
    {
        if (_stateWatchers.Add(element)) { _stateDirty = true; }
    }

    internal void WatchValues(ViewElement element)
    {
        if (_valueWatchers.Add(element)) { _valueDirty = true; }
    }

    internal void ResetAnimations(ViewElement target) => Animations.Reset(target);

    public void Once(Action action) => _queuedOnce.Add(action);

    internal bool BatchingLayout => _layoutBatch > 0;

    internal void BeginLayoutBatch() => _layoutBatch++;

    internal void EndLayoutBatch()
    {
        if (--_layoutBatch > 0) { return; }
        if (_flushingShares) { return; }

        _flushingShares = true;

        try
        {
            while (_batchedShares.Count > 0)
            {
                Controls.StackElement[] pending = [.. _batchedShares];
                _batchedShares.Clear();

                for (int i = 0; i < pending.Length; i++) { pending[i].FlushShares(); }
            }
        }
        finally
        {
            _flushingShares = false;
        }
    }

    internal void QueueShares(Controls.StackElement stack) => _batchedShares.Add(stack);

    internal void RequestLayout(ViewElement origin)
    {
        for (ViewElement? current = origin; current is not null; current = current.Parent) { MarkLayoutDirty(current); }

        MarkSubtree(origin);

        if (_layoutRequested) { return; }

        _layoutRequested = true;
        Once(FlushLayout);
    }

    private void FlushLayout()
    {
        _layoutRequested = false;

        if (Root is { } root) { LayoutFlush.Apply(root.Control); }
    }

    private static void MarkLayoutDirty(ViewElement element)
    {
        if (element.Control is GUILayoutGroup group) { group.NeedsToRecalculate = true; }

        if (element.Control is GUIListBox listBox)
        {
            listBox.ChildrenNeedRecalculation = true;
            listBox.DimensionsNeedRecalculation = true;
        }
    }

    private static void MarkSubtree(ViewElement element)
    {
        MarkLayoutDirty(element);

        foreach (ViewElement child in element.ChildElements) { MarkSubtree(child); }
    }

    internal void FocusOnOpen(Action focus) => _focusOnOpen.Add(focus);

    public void FocusAfterClick(Action focus) => FocusOnClickEnd(focus);

    public void RequestFocus()
    {
        for (int i = 0; i < _focusOnOpen.Count; i++) { FocusOnClickEnd(_focusOnOpen[i]); }
    }

    internal void RegisterInput(Input.KeyBinding binding)
    {
        _inputs.Add(binding);

        foreach (KeyCode key in binding.Keys)
        {
            if (!_byKey.TryGetValue(key, out List<Input.KeyBinding>? sharers)) { _byKey[key] = sharers = []; }

            sharers.Add(binding);
        }
    }

    public void RunInputBindings()
    {
        for (int i = 0; i < _inputs.Count; i++)
        {
            Input.KeyBinding binding = _inputs[i];

            if (!binding.Owner.Control.Visible) { continue; }
            if (binding.RequiresNoTextInput && GUI.KeyboardDispatcher.Subscriber is not null) { continue; }
            if (!binding.Held() || !binding.Pressed() || Outranked(binding)) { continue; }

            UiGuard.Run(binding, static fired => fired.Fire());
        }
    }

    public void RunFrameActions()
    {
        for (int i = 0; i < _scopes.Count; i++) { _scopes[i].RunFrameActions(); }

        DrainOnce();
        PollWatches();
        Animations.Tick();
    }

    private void DrainOnce()
    {
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

    private void PollWatches()
    {
        if (_stateDirty)
        {
            _stateSnapshot = [.. _stateWatchers];
            _stateDirty = false;
        }

        for (int i = 0; i < _stateSnapshot.Length; i++)
        {
            ViewElement element = _stateSnapshot[i];

            if (element.IsPaused) { continue; }

            UiGuard.Run(element, static watched => watched.PollState());
        }

        if (_valueDirty)
        {
            _valueSnapshot = [.. _valueWatchers];
            _valueDirty = false;
        }

        for (int i = 0; i < _valueSnapshot.Length; i++)
        {
            ViewElement element = _valueSnapshot[i];

            if (element.IsPaused) { continue; }

            UiGuard.Run(element, static watched => watched.PollValues());
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

        Animations.StopAll();

        foreach (ViewElement element in _stateWatchers) { element.ClearWatchers(); }
        foreach (ViewElement element in _valueWatchers) { element.ClearWatchers(); }

        _stateWatchers.Clear();
        _valueWatchers.Clear();
        _stateSnapshot = [];
        _valueSnapshot = [];
        _stateDirty = true;
        _valueDirty = true;

        foreach (ViewScope scope in scopes)
        {
            UiGuard.Run(scope.Dispose);
        }

        _queuedOnce.Clear();
        _firingOnce.Clear();
        _focusOnOpen.Clear();
        _overlays.Clear();
        _inputs.Clear();
        _byKey.Clear();
        _batchedShares.Clear();
        _layoutBatch = 0;
        _flushingShares = false;
        _layoutRequested = false;
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
        foreach (KeyCode key in binding.Keys)
        {
            if (!_byKey.TryGetValue(key, out List<Input.KeyBinding>? sharers)) { continue; }

            for (int i = 0; i < sharers.Count; i++)
            {
                Input.KeyBinding other = sharers[i];

                if (ReferenceEquals(other, binding) || other.Specificity <= binding.Specificity) { continue; }
                if (!other.Owner.Control.Visible) { continue; }
                if (other.Held() && other.Pressed()) { return true; }
            }
        }

        return false;
    }

    private static void Detach(GUIComponent control)
    {
        control.RemoveFromGUIUpdateList();
        control.RectTransform.Parent = null;
    }
}
