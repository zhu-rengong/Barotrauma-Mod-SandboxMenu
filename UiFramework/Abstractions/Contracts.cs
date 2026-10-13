namespace UiFramework.Abstractions;

internal interface IValueAssignment
{
    PropertyMetadata Property { get; }

    void SetText(string text);

    void SetValue(object? value, string source);

    void Bind(BindingOptions options);

    void BindMulti(IReadOnlyList<BindingOptions> bindings, string? converter, string? parameter);

    void SetResource(string key, bool reevaluate);

    void Report(string message);
}

internal interface IStyleResource
{
    string? Key { get; }
}

internal interface IPauseable
{
    bool IsPaused { get; }
}

internal interface IViewHost
{
    MarkupDiagnostics Diagnostics { get; }

    void Own(IDisposable disposable);
}

internal interface IStoryboard
{
    bool IsEmpty { get; }

    bool SharesProperty(IStoryboard other);
}

internal interface IAnimationPlayer
{
    bool Tick(float delta);

    void Stop();
}

internal interface IStoryboardRunner
{
    IAnimationPlayer Play(IStoryboard storyboard, IViewElement target, IPauseable? scope, IViewHost host);
}

internal interface IElementAnimations
{
    void Tick();

    void Reset(IViewElement target);

    void StopAll();
}

internal interface IViewRuntime : IElementAnimations, IStoryboardRunner
{
}

internal interface IViewElement
{
    MarkupNode? Node { get; }

    object? DataContext { get; }

    bool TryApplyValue(string property, object? value, ValueSource source, out string message);

    bool TryApplyMarkup(string property, MarkupValue value, ValueSource source, out string message);

    bool TryReadValue(string property, out object? value);

    bool HasBaseValue(string property);

    void RememberBaseValue(string property, object? value);

    void RememberRestoreValue(string property, object? value);

    void RestoreBaseValue(string property);

    bool CanAnimate(string property, out string message);

    bool WatchValue(string property, Action<object?> changed);

    bool Listen(string name, Action handler);

    bool CanOverride(string property, out string reason);

    bool ApplyOverride(object source, int priority, string property, MarkupValue value, out string message);

    void RemoveOverride(object source, string property);
}
