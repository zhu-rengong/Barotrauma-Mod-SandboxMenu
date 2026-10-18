namespace SandboxMenu.UI.Models;

internal static class ContentCaches
{
    internal static void Drop()
    {
        ItemPrefabCatalog.Reset();
        AfflictionPrefabCatalog.Reset();
        TalentPrefabCatalog.Reset();
        ItemDisplay.Reset();
        AfflictionDisplay.Reset();
        TalentDisplay.Reset();
        PropertyCatalog.Reset();
        ContainerRules.Reset();
    }
}
