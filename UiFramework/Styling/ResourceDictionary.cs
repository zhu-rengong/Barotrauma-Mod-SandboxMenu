namespace UiFramework.Styling;

internal sealed class ResourceDictionary
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    internal ResourceDictionary? Parent { get; set; }

    internal object? Find(string key)
        => _values.TryGetValue(key, out object? value) ? value : Parent?.Find(key);

    internal void Set(string key, object? value)
    {
        if (value is null) { _values.Remove(key); }
        else { _values[key] = value; }
    }
}
