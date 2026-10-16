using HarmonyLib;

namespace SandboxMenu;

[HarmonyPatch(typeof(ContentPackageManager.EnabledPackages), nameof(ContentPackageManager.EnabledPackages.SortContent))]
internal static class ContentPackageSortPatch
{
    private static void Postfix()
    {
        try
        {
            MenuWindow.Invalidated();
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Rebuilding the sandbox menu content failed: {e}");
        }
    }
}

[HarmonyPatch(typeof(TextManager), nameof(TextManager.IncrementLanguageVersion))]
internal static class LanguageVersionPatch
{
    private static void Postfix()
    {
        try
        {
            MenuWindow.Invalidated();
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Reloading the sandbox menu failed: {e}");
        }
    }
}
