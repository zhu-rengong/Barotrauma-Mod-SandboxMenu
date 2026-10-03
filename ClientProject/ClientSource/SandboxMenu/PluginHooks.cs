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
        if (Screen.Selected != GameMain.GameScreen) { return; }

        if (MenuHost.Current is not { IsOpen: true } menu) { return; }

        Guard.Run(menu.AddToUpdateList);
    }

    internal static void GameModeDraw(GameMode mode, SpriteBatch spriteBatch)
    {
        if (Screen.Selected != GameMain.GameScreen) { return; }

        Guard.Run(spriteBatch, SpawnPointPicker.DrawHint);
    }

    internal static void PostUpdate(float deltaTime)
    {
        Guard.Run(ContentReload.Poll);

        // A queued screenshot is served here, once the frame's GUI update has run through and no batch is open.
        Guard.Run(() => MenuHost.Current?.ServePendingScreenshot());

        if (Screen.Selected != GameMain.GameScreen) { return; }

        Guard.Run(HandleHotkeys);
    }

    // The host's escape hook: returning true keeps the game from opening its own pause menu. The picker is still up
    // here and is what escape closes, in its own update right after.
    internal static bool HandleEscapeKey()
    {
        if (Screen.Selected != GameMain.GameScreen) { return false; }
        if (DebugConsole.IsOpen || GUI.SettingsMenuOpen || GUI.PauseMenuOpen) { return false; }

        if (SpawnPointPicker.IsActive) { return true; }

        return MenuHost.Current?.HandleEscape() ?? false;
    }

    private static void HandleHotkeys()
    {
        if (SpawnPointPicker.IsActive)
        {
            SpawnPointPicker.Update();
            return;
        }

        if (DebugConsole.IsOpen || GUI.SettingsMenuOpen || GUI.PauseMenuOpen) { return; }

        // A text box takes the keyboard while it is selected, so the hotkeys stand back; the menu's own keys work it out.
        if (GUI.KeyboardDispatcher.Subscriber is null)
        {
            if (Plugin.ToggleKey.IsHit())
            {
                MenuHost.Instance.Toggle();
                return;
            }

            if (Plugin.GiveKey.IsHit()) { MenuHost.Instance.SpawnIntoInventory(); }
        }

        MenuHost.Current?.HandleKeys();
    }
}
