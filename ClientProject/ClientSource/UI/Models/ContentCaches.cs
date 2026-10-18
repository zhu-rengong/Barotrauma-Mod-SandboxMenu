namespace SandboxMenu.UI.Models;

internal static class ContentCaches
{
    internal static void Drop()
    {
        ItemPrefabCatalog.Reset();
        AfflictionPrefabCatalog.Reset();
        ItemDisplay.Reset();
        AfflictionDisplay.Reset();
        PropertyCatalog.Reset();
        ContainerRules.Reset();
    }
}
