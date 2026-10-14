namespace SandboxMenu.UI.Models;

internal static class CachedContent
{
    internal static void Drop()
    {
        ItemPrefabCatalog.Reset();
        AfflictionPrefabCatalog.Reset();
        ItemDisplay.Reset();
        AfflictionDisplay.Reset();
        PropertyCatalog.Reset();
        StatusTones.Reset();
    }
}
