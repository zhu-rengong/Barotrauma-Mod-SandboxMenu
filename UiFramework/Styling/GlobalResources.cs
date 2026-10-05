namespace UiFramework.Styling;

// The resources the mod puts above every view: the implicit styles its markup falls back on. Handed over at
// startup and given back at shutdown, so nothing here outlives the plugin.
internal static class GlobalResources
{
    private static ResourceDictionary? _resources;

    static GlobalResources() => UiLifetime.Unloading += () => _resources = null;

    internal static void Register(ResourceDictionary resources) => _resources = resources;

    internal static object? Find(string key) => _resources?.Find(key);
}
