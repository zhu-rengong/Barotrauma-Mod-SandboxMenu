using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu;

internal static class PluginHooks
{
    internal static readonly PluginGameModeAddToGUIUpdateListDelegate AddToGUIUpdateListHook = AddToGUIUpdateList;

    internal static readonly PluginGameModeDrawDelegate GameModeDrawHook = GameModeDraw;

    internal static readonly PluginOnPostUpdateDelegate PostUpdateHook = PostUpdate;

    internal static readonly PluginHandleEscapeKeyDelegate EscapeKeyHook = HandleEscapeKey;

    internal static void AddToGUIUpdateList(GameMode mode)
    {
        try
        {
            if (Screen.Selected != GameMain.GameScreen) { return; }
            if (MenuWindow.Current is not { IsOpen: true } menu) { return; }

            menu.AddToUpdateList();
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Updating the sandbox menu failed: {e}");
        }
    }

    internal static void GameModeDraw(GameMode mode, SpriteBatch spriteBatch)
    {
        try
        {
            if (Screen.Selected != GameMain.GameScreen) { return; }

            SpawnPointPicker.DrawHint(spriteBatch);
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Drawing the spawn point hint failed: {e}");
        }
    }

    internal static void PostUpdate(float deltaTime)
    {
        try
        {
            ContentReload.Poll();
            MenuWindow.Current?.ServePendingScreenshot();

            if (Screen.Selected != GameMain.GameScreen) { return; }

            HandleHotkeys();
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"The sandbox menu's post-update failed: {e}");
        }
    }

    internal static bool HandleEscapeKey()
    {
        if (Screen.Selected != GameMain.GameScreen) { return false; }
        if (DebugConsole.IsOpen || GUI.SettingsMenuOpen || GUI.PauseMenuOpen) { return false; }

        if (SpawnPointPicker.IsActive) { return true; }

        return MenuWindow.Current?.HandleEscape() ?? false;
    }

    private static void HandleHotkeys()
    {
        if (SpawnPointPicker.IsActive)
        {
            SpawnPointPicker.Update();
            return;
        }

        if (DebugConsole.IsOpen || GUI.SettingsMenuOpen || GUI.PauseMenuOpen) { return; }

        if (GUI.KeyboardDispatcher.Subscriber is null)
        {
            if (Plugin.ToggleKey.IsHit())
            {
                MenuWindow.Instance.Toggle();
                return;
            }

            if (Plugin.GiveKey.IsHit()) { MenuWindow.Instance.SpawnIntoInventory(); }
        }

        MenuWindow.Current?.HandleKeys();
    }
}
