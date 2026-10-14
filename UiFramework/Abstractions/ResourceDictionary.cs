namespace UiFramework.Abstractions;

public sealed class ResourceDictionary
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    public object? Find(string key) => _values.GetValueOrDefault(key);

    public void Set(string key, object? value)
    {
        if (value is null) { _values.Remove(key); }
        else { _values[key] = value; }
    }

    internal IEnumerable<(string Key, object Value)> Entries()
    {
        foreach ((string key, object value) in _values) { yield return (key, value); }
    }
}

internal static class GlobalResources
{
    private static ResourceDictionary? _resources;

    internal static void Register(ResourceDictionary resources) => _resources = resources;

    internal static object? Find(string key) => _resources?.Find(key);
}
