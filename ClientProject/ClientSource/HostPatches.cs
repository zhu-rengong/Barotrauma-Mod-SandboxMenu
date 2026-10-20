using HarmonyLib;

namespace SandboxMenu;

[HarmonyPatch(typeof(ContentPackageManager.EnabledPackages), nameof(ContentPackageManager.EnabledPackages.SortContent))]
internal static class ContentPackageSortPatch
{
    private static void Postfix()
    {
        try
        {
            SandboxMenuHost.Invalidated();
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Rebuilding the sandbox menu content failed: {e}");
        }
    }
}
