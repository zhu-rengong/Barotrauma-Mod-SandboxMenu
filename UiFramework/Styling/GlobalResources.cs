namespace UiFramework.Styling;

internal static class GlobalResources
{
    private static ResourceDictionary? _resources;

    internal static void Register(ResourceDictionary resources) => _resources = resources;

    internal static object? Find(string key) => _resources?.Find(key);
}
