using System.Windows.Input;

namespace UiFramework;

internal enum VisualState
{
    None = 0,
    MouseOver = 1,
    Pressed = 2,
    Selected = 4
}

internal readonly record struct Override(object Source, int Priority, object? Value);

internal sealed class ValueWatch(string name, Func<object, object?>? read)
{
    private readonly List<Action<object?>> _watchers = [];

    internal string Name { get; } = name;

    internal bool HasLast { get; private set; }

    internal object? Last { get; private set; }

    internal void Add(Action<object?> watcher) => _watchers.Add(watcher);

    internal void Notify(object? value)
    {
        Last = value;
        HasLast = true;

        for (int i = 0; i < _watchers.Count; i++)
        {
            Action<object?> watcher = _watchers[i];

            try
            {
                watcher(value);
            }
            catch (Exception e)
            {
                DebugConsole.AddWarning($"Watching '{Name}' failed: {e}");
            }
        }
    }

    internal bool TryRead(ViewElement element, out object? value)
    {
        if (read is null)
        {
            value = null;
            return false;
        }

        try
        {
            value = read(element);
            return true;
        }
        catch (Exception)
        {
            value = null;
            return false;
        }
    }
}

public abstract class ViewElement(GUIComponent control) : IViewElement, IDisposable
{
    private bool _released;

    private readonly Dictionary<string, object?> _attached = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _baseValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _restoreValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ValueWatch> _valueWatches = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Override>> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Action>> _listeners = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Action<object?>>?[] _visualWatchers = new List<Action<object?>>?[3];
    private readonly List<(string Property, object? Value)> _resetScratch = [];
    private readonly List<ViewElement> _children = [];

    private VisualState _visuals;

    private Length? _width;
    private Length? _height;
    private Anchor? _align;
    private Insets? _margin;
    private string? _skin;
    private bool _skinRead;

    public GUIComponent Control { get; } = control;

    internal IReadOnlyList<ViewElement> ChildElements => _children;

    internal bool IsReleased => _released;

    internal int SubscriptionCount
    {
        get
        {
            int count = _valueWatches.Count + _listeners.Count + _overrides.Count;

            for (int i = 0; i < _visualWatchers.Length; i++) { count += _visualWatchers[i]?.Count ?? 0; }

            return count;
        }
    }

    internal void RememberContent(ViewElement child) => _children.Add(child);

    internal MarkupNode? Node { get; set; }

    internal string? ElementName { get; set; }

    internal ViewScope? Scope { get; set; }

    public ViewContext? Context { get; internal set; }

    internal List<Input.KeyBinding>? Bindings { get; set; }

    public object? DataContext { get; set; }

    public ResourceDictionary? Resources { get; set; }

    public ViewElement? Parent { get; set; }

    [ElementProperty]
    public bool Visible
    {
        set
        {
            if (Control.Visible == value) { return; }

            Control.Visible = value;
            Parent?.ChildVisibilityChanged(this);
            Parent?.ChildLayoutChanged(this);
        }
    }

    [ElementProperty]
    public bool Drag
    {
        set
        {
            if (value) { Context?.AttachDrag(Control.RectTransform); }
        }
    }

    [ElementProperty]
    public Color Color { set => Control.Color = value; }

    [ElementProperty]
    public Color HoverColor { set => Control.HoverColor = value; }

    [ElementProperty]
    public Color PressedColor { set => Control.PressedColor = value; }

    [ElementProperty]
    public Color SelectedColor { set => Control.SelectedColor = value; }

    [ElementProperty]
    public Color DisabledColor { set => Control.DisabledColor = value; }

    [ElementProperty]
    public Color OutlineColor
    {
        get => Control.OutlineColor;
        set => Control.OutlineColor = value;
    }

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal Length Width => _width ??= Length.Parse(Node?.Text("Width"), Length.Fill);

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal Length Height => _height ??= Length.Parse(Node?.Text("Height"), Length.Fill);

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal Anchor Align => _align ??= ViewMarkup.AnchorOf(Node?.Text("Align"), Anchor.TopLeft);

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal Insets Margin => _margin ??= Node?.Margin ?? Insets.None;

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal Insets Padding
        => Control is GUITextBlock block
            ? new Insets((int)block.Padding.Y, (int)block.Padding.Z, (int)block.Padding.W, (int)block.Padding.X)
            : Insets.None;

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal string? Skin
    {
        get
        {
            if (_skinRead) { return _skin; }

            _skin = Node?.Text("Skin");
            _skinRead = true;

            return _skin;
        }
    }

    internal bool IsPaused => Scope is { IsPaused: true };

    public virtual void AddContent(ViewElement child) => throw new NotSupportedException($"{GetType().Name} takes no content");

    public virtual void ChildVisibilityChanged(ViewElement child)
    {
    }

    public virtual void ChildLayoutChanged(ViewElement child)
    {
    }

    public virtual RectTransform ContentParent => Control.RectTransform;

    internal void SetAttached(string name, object? value) => _attached[name] = value;

    internal object? Attached(string name) => _attached.GetValueOrDefault(name);

    internal int AttachedInt(string name, int fallback)
        => Attached(name) is int value ? value
            : Attached(name) is string text && int.TryParse(text, out int parsed) ? parsed
            : fallback;

    internal void RememberBaseValue(string property, object? value) => _baseValues[property] = value;

    internal bool HasBaseValue(string property) => _baseValues.ContainsKey(property);

    internal void RememberRestoreValue(string property, object? value)
    {
        if (_restoreValues.ContainsKey(property)) { return; }

        _restoreValues[property] = value;
    }

    internal void ResetAnimatedValues()
    {
        Context?.ResetAnimations(this);

        if (_restoreValues.Count == 0) { return; }

        _resetScratch.Clear();

        foreach ((string property, object? value) in _restoreValues) { _resetScratch.Add((property, value)); }

        _restoreValues.Clear();

        for (int i = 0; i < _resetScratch.Count; i++)
        {
            TryApplyValue(_resetScratch[i].Property, _resetScratch[i].Value, ValueSource.Trigger, out _);
        }

        _resetScratch.Clear();
    }

    internal bool TryReadValue(string property, out object? value)
    {
        if (_baseValues.TryGetValue(property, out value)) { return true; }

        PropertyMetadata? metadata = Property(property);

        if (metadata?.Read is { } read)
        {
            try
            {
                value = read(this);
                return true;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }

        value = null;
        return false;
    }

    internal void RestoreBaseValue(string property)
    {
        if (_baseValues.TryGetValue(property, out object? value))
        {
            TryApplyValue(property, value, ValueSource.Base, out _);
            return;
        }

        if (_restoreValues.TryGetValue(property, out value))
        {
            TryApplyValue(property, value, ValueSource.Trigger, out _);
            return;
        }

        PropertyMetadata? metadata = Property(property);

        if (metadata?.Read is not { } read) { return; }

        try
        {
            TryApplyValue(property, read(this), ValueSource.Trigger, out _);
        }
        catch (Exception)
        {
        }
    }

    internal bool ApplyOverride(object source, int priority, string property, MarkupValue value, out string message)
    {
        if (!_baseValues.ContainsKey(property) && ReadCurrent(property, out object? before)) { RememberBaseValue(property, before); }

        if (!TryApplyMarkup(property, value, ValueSource.Trigger, out message)) { return false; }

        ReadCurrent(property, out object? applied);

        if (!_overrides.TryGetValue(property, out List<Override>? stack))
        {
            _overrides[property] = stack = [];
        }

        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(stack[i].Source, source)) { stack.RemoveAt(i); }
        }

        stack.Add(new Override(source, priority, applied));
        ResolveOverride(property);

        return true;
    }

    internal void RemoveOverride(object source, string property)
    {
        if (!_overrides.TryGetValue(property, out List<Override>? stack)) { return; }

        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(stack[i].Source, source)) { stack.RemoveAt(i); }
        }

        ResolveOverride(property);
    }

    internal bool TryApplyValue(string property, object? value, ValueSource source, out string message)
    {
        if (Property(property) is not { } metadata)
        {
            message = $"'{property}' is not a property of <{ElementName ?? GetType().Name}>";
            return false;
        }

        if (metadata.Phase == ApplyPhase.Construct)
        {
            message = $"'{property}' is decided while <{ElementName ?? GetType().Name}> is built";
            return false;
        }

        (object? converted, bool ok) = ValueConversion.Convert(value, metadata.ValueType);

        if (!ok)
        {
            message = $"'{value}' is not a {metadata.ValueType.Name}";
            return false;
        }

        try
        {
            metadata.Apply(this, converted);
        }
        catch (Exception e)
        {
            message = e.Message;
            return false;
        }

        if (source == ValueSource.Base) { RememberBaseValue(metadata.Name, converted); }

        NotifyValueChanged(metadata.Name, converted);
        message = string.Empty;
        return true;
    }

    internal bool TryApplyMarkup(string property, MarkupValue value, ValueSource source, out string message)
    {
        PropertyMetadata? metadata = Property(property);

        if (metadata is null)
        {
            message = $"'{property}' is not a property of <{ElementName ?? GetType().Name}>";
            return false;
        }

        if (metadata.Phase == ApplyPhase.Construct)
        {
            message = $"'{property}' is decided while <{ElementName ?? GetType().Name}> is built";
            return false;
        }

        if (value.Extension is null)
        {
            if (value.Raw.Length == 0 && !metadata.KeyText)
            {
                message = "an empty value";
                return false;
            }

            new PropertyAssignment(Context!, Node!, this, metadata) { Source = source }.SetText(value.Raw);
            message = string.Empty;
            return true;
        }

        if (Context is not { } view)
        {
            message = "there is no view to bind against";
            return false;
        }

        try
        {
            value.Extension.Apply(new PropertyAssignment(view, Node!, this, metadata) { Source = source });
        }
        catch (Exception e)
        {
            message = e.Message;
            return false;
        }

        message = string.Empty;
        return true;
    }

    internal bool CanOverride(string property, out string reason)
    {
        if (Property(property) is not { } metadata)
        {
            reason = $"'{property}' is not a property of <{ElementName ?? GetType().Name}>";
            return false;
        }

        if (metadata.Phase == ApplyPhase.Construct)
        {
            reason = $"'{property}' is decided while <{ElementName ?? GetType().Name}> is built";
            return false;
        }

        if (metadata.Read is null)
        {
            reason = $"'{property}' cannot be read back, so a trigger cannot take its value back";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    internal bool CanAnimate(string property, out string message)
    {
        if (Property(property) is not { } metadata)
        {
            message = $"'{property}' is not a property of <{ElementName ?? GetType().Name}>";
            return false;
        }

        if (metadata.Phase == ApplyPhase.Construct)
        {
            message = $"'{property}' is decided while <{ElementName ?? GetType().Name}> is built and cannot be animated";
            return false;
        }

        if (!metadata.CanAnimate)
        {
            message = $"a {metadata.ValueType.Name} is not something an animation can interpolate";
            return false;
        }

        message = string.Empty;
        return true;
    }

    internal bool WatchValue(string property, Action<object?> changed)
    {
        if (Visual(property) is { } visual)
        {
            int index = Index(visual);

            (_visualWatchers[index] ??= []).Add(changed);
            Context?.WatchState(this);

            Guard(changed, IsVisual(visual));

            return true;
        }

        if (Property(property) is not { } metadata) { return false; }

        if (!_valueWatches.TryGetValue(property, out ValueWatch? watch))
        {
            watch = new ValueWatch(property, metadata.Read);
            _valueWatches[property] = watch;

            Context?.WatchValues(this);
        }

        watch.Add(changed);

        if (watch.TryRead(this, out object? current)) { watch.Notify(current); }

        return true;
    }

    internal void NotifyValueChanged(string property, object? value)
    {
        if (_valueWatches.TryGetValue(property, out ValueWatch? watch)) { watch.Notify(value); }
    }

    internal void PollValues()
    {
        foreach (ValueWatch watch in _valueWatches.Values)
        {
            if (!watch.TryRead(this, out object? current)) { continue; }
            if (watch.HasLast && Equals(watch.Last, current)) { continue; }

            watch.Notify(current);
        }
    }

    internal bool Listen(string name, Action handler)
    {
        if (!IsEvent(name)) { return false; }

        if (!_listeners.TryGetValue(name, out List<Action>? listeners))
        {
            _listeners[name] = listeners = [];
        }

        listeners.Add(handler);

        return true;
    }

    protected internal void RaiseEvent(string name)
    {
        if (!_listeners.TryGetValue(name, out List<Action>? listeners)) { return; }

        for (int i = 0; i < listeners.Count; i++)
        {
            Action listener = listeners[i];
            UiGuard.Run(listener);
        }
    }

    internal void PollState()
    {
        GUIComponent.ComponentState state = Control.State;

        SetVisual(
            VisualState.MouseOver,
            state is GUIComponent.ComponentState.Hover or GUIComponent.ComponentState.HoverSelected or GUIComponent.ComponentState.Pressed);

        SetVisual(VisualState.Pressed, state is GUIComponent.ComponentState.Pressed);
        SetVisual(VisualState.Selected, Control.Selected);
    }

    public void Dispose()
    {
        if (_released) { return; }

        _released = true;
        OnRelease();
        ClearWatchers();
    }

    protected virtual void OnRelease()
    {
    }

    protected void StopEveryFrame(Action action) => Scope?.Remove(action);

    protected void StopEveryFrame(Func<bool> action) => Scope?.Remove(action);

    internal void ClearWatchers()
    {
        _valueWatches.Clear();
        _overrides.Clear();
        _restoreValues.Clear();
        _listeners.Clear();

        for (int i = 0; i < _visualWatchers.Length; i++) { _visualWatchers[i]?.Clear(); }
    }

    MarkupNode? IViewElement.Node => Node;

    object? IViewElement.DataContext => DataContext;

    bool IViewElement.TryApplyValue(string property, object? value, ValueSource source, out string message)
        => TryApplyValue(property, value, source, out message);

    bool IViewElement.TryApplyMarkup(string property, MarkupValue value, ValueSource source, out string message)
        => TryApplyMarkup(property, value, source, out message);

    bool IViewElement.TryReadValue(string property, out object? value) => TryReadValue(property, out value);

    bool IViewElement.HasBaseValue(string property) => HasBaseValue(property);

    void IViewElement.RememberBaseValue(string property, object? value) => RememberBaseValue(property, value);

    void IViewElement.RememberRestoreValue(string property, object? value) => RememberRestoreValue(property, value);

    void IViewElement.RestoreBaseValue(string property) => RestoreBaseValue(property);

    bool IViewElement.CanAnimate(string property, out string message) => CanAnimate(property, out message);

    bool IViewElement.WatchValue(string property, Action<object?> changed) => WatchValue(property, changed);

    bool IViewElement.Listen(string name, Action handler) => Listen(name, handler);

    bool IViewElement.CanOverride(string property, out string reason) => CanOverride(property, out reason);

    bool IViewElement.ApplyOverride(object source, int priority, string property, MarkupValue value, out string message)
        => ApplyOverride(source, priority, property, value, out message);

    void IViewElement.RemoveOverride(object source, string property) => RemoveOverride(source, property);

    private void SetVisual(VisualState visual, bool value)
    {
        if (IsVisual(visual) == value) { return; }

        _visuals = value ? _visuals | visual : _visuals & ~visual;

        if (visual == VisualState.MouseOver) { RaiseEvent(value ? "MouseEnter" : "MouseLeave"); }
        else if (visual == VisualState.Pressed) { RaiseEvent(value ? "Pressed" : "Released"); }

        if (_visualWatchers[Index(visual)] is not { } watchers) { return; }

        for (int i = 0; i < watchers.Count; i++) { Guard(watchers[i], value); }
    }

    private bool IsVisual(VisualState visual) => (_visuals & visual) != 0;

    private static void Guard(Action<object?> watcher, object? value)
    {
        try
        {
            watcher(value);
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Reacting to a property change failed: {e}");
        }
    }

    private void ResolveOverride(string property)
    {
        if (!_overrides.TryGetValue(property, out List<Override>? stack) || stack.Count == 0)
        {
            _overrides.Remove(property);
            RestoreBaseValue(property);
            return;
        }

        int best = 0;

        for (int i = 1; i < stack.Count; i++)
        {
            if (stack[i].Priority >= stack[best].Priority) { best = i; }
        }

        TryApplyValue(property, stack[best].Value, ValueSource.Trigger, out _);
    }

    private bool ReadCurrent(string property, out object? value)
    {
        if (Property(property)?.Read is { } read)
        {
            try
            {
                value = read(this);
                return true;
            }
            catch (Exception)
            {
            }
        }

        value = null;
        return false;
    }

    private static int Index(VisualState visual) => visual switch
    {
        VisualState.MouseOver => 0,
        VisualState.Pressed => 1,
        _ => 2
    };

    private static VisualState? Visual(string name)
        => name switch
        {
            _ when string.Equals(name, "IsMouseOver", StringComparison.OrdinalIgnoreCase) => VisualState.MouseOver,
            _ when string.Equals(name, "IsPressed", StringComparison.OrdinalIgnoreCase) => VisualState.Pressed,
            _ when string.Equals(name, "IsSelected", StringComparison.OrdinalIgnoreCase) => VisualState.Selected,
            _ => null
        };

    private static bool IsEvent(string name)
        => name is "Loaded" or "Click" or "MouseEnter" or "MouseLeave" or "Pressed" or "Released" or "SecondaryPressed"
            or "TextChanged";

    private PropertyMetadata? Property(string name)
    {
        if (ElementName is { } element && ElementRegistry.Property(element, name) is { } declared) { return declared; }

        return AttachedProperties.Find(name);
    }

    public override string ToString() => GetType().Name;
}

internal interface IPropertyObserver
{
    void Observe(string property, Action<object?> changed);
}

internal interface ICommandElement
{
    void SetCommand(ICommand? command);

    void RefreshCanExecute();
}
