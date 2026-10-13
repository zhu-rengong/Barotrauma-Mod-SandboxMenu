namespace UiFramework.Abstractions;

public enum BindingMode
{
    OneWay,
    TwoWay,

    OneTime,

    OneWayToSource
}

public enum ApplyPhase
{
    Construct,
    Live
}

internal enum ValueSource
{
    Base,
    Trigger,
    Animation
}

internal sealed record PropertyMetadata(
    string Name,
    Type ValueType,
    ApplyPhase Phase,
    BindingMode DefaultMode,
    bool SettableFromStyle,
    bool KeyText,
    Action<object, object?> Apply,
    Func<object, object?>? Read)
{
    internal bool CanAnimate
        => ValueType == typeof(float)
            || ValueType == typeof(double)
            || ValueType == typeof(int)
            || ValueType == typeof(Color)
            || ValueType == typeof(Length)
            || ValueType == typeof(Insets);
}
