using System.Windows.Input;
using Microsoft.Xna.Framework.Graphics;
using UiFramework.Styling;
using KeyCode = Microsoft.Xna.Framework.Input.Keys;

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
    private readonly List<Animation.StoryboardPlayer> _players = [];
    private readonly HashSet<ViewElement> _stateWatchers = [];
    private readonly HashSet<ViewElement> _valueWatchers = [];
    private readonly List<ViewElement> _watching = [];

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

    public bool Interactive { get; set; } = true;

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

    internal ViewScope? CurrentScope => Current;

    public void EveryFrame(Action action) => Current?.EveryFrame(action);

    public void EveryFrame(Func<bool> action) => Current?.EveryFrame(action);

    internal void WatchState(ViewElement element) => _stateWatchers.Add(element);

    internal void WatchValues(ViewElement element) => _valueWatchers.Add(element);

    internal Animation.StoryboardPlayer Play(Animation.Storyboard storyboard, ViewElement target, ViewScope? scope)
    {
        Animation.StoryboardPlayer player = new(storyboard, target, scope ?? Current, this);

        target.TrackPlayer(player);
        _players.Add(player);

        return player;
    }

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

            UiGuard.Run(binding, static fired => fired.Fire());
        }
    }

    public void RunFrameActions()
    {
        for (int i = 0; i < _scopes.Count; i++) { _scopes[i].RunFrameActions(); }

        DrainOnce();
        PollWatches();
        TickAnimations();
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
        if (_stateWatchers.Count > 0)
        {
            _watching.AddRange(_stateWatchers);

            for (int i = 0; i < _watching.Count; i++)
            {
                ViewElement element = _watching[i];

                if (element.IsPaused) { continue; }

                UiGuard.Run(element, static watched => watched.PollState());
            }

            _watching.Clear();
        }

        if (_valueWatchers.Count == 0) { return; }

        _watching.AddRange(_valueWatchers);

        for (int i = 0; i < _watching.Count; i++)
        {
            ViewElement element = _watching[i];

            if (element.IsPaused) { continue; }

            UiGuard.Run(element, static watched => watched.PollValues());
        }

        _watching.Clear();
    }

    private void TickAnimations()
    {
        if (_players.Count == 0) { return; }

        float delta = Animation.AnimationClock.Delta;

        for (int i = _players.Count - 1; i >= 0; i--)
        {
            Animation.StoryboardPlayer player = _players[i];

            if (!UiGuard.TryRun((Player: player, Delta: delta), static state => state.Player.Tick(state.Delta)))
            {
                player.Stop();
                _players.RemoveAt(i);
            }
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

        foreach (Animation.StoryboardPlayer player in _players) { UiGuard.Run(player.Stop); }

        _players.Clear();

        foreach (ViewElement element in _stateWatchers) { element.ClearWatchers(); }
        foreach (ViewElement element in _valueWatchers) { element.ClearWatchers(); }

        _stateWatchers.Clear();
        _valueWatchers.Clear();

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
        for (int i = 0; i < _inputs.Count; i++)
        {
            Input.KeyBinding other = _inputs[i];

            if (ReferenceEquals(other, binding) || other.Specificity <= binding.Specificity) { continue; }
            if (!other.Owner.Control.Visible || !other.Held() || !other.Pressed()) { continue; }
            if (Shares(binding, other)) { return true; }
        }

        return false;
    }

    private static bool Shares(Input.KeyBinding left, Input.KeyBinding right)
    {
        KeyCode[] mine = left.Keys;
        KeyCode[] theirs = right.Keys;

        for (int i = 0; i < mine.Length; i++)
        {
            for (int j = 0; j < theirs.Length; j++)
            {
                if (mine[i] == theirs[j]) { return true; }
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
