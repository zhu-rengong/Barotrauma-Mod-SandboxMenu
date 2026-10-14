namespace UiFramework;

internal struct ShapeSignature<T> where T : struct, IEquatable<T>
{
    private T _value;
    private bool _has;

    internal bool Matches(in T candidate) => _has && _value.Equals(candidate);

    internal void Set(in T signature)
    {
        _value = signature;
        _has = true;
    }

    internal void Reset()
    {
        _value = default;
        _has = false;
    }
}
