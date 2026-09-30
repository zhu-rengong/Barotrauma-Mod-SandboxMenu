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

        if (SandboxMenuWindow.Current is not { IsOpen: true } menu) { return; }

        Guard.Run("Menu update registration failed", menu.AddToUpdateList);
    }

    internal static void GameModeDraw(GameMode mode, SpriteBatch spriteBatch)
    {
        if (Screen.Selected != GameMain.GameScreen) { return; }

        Guard.Run("Picker hint drawing failed", () => SpawnLocationPicker.DrawHint(spriteBatch));
    }

    internal static void PostUpdate(float deltaTime)
    {
        Guard.Run("Watching the content packages failed", ContentWatch.Watch);

        // A queued screenshot is served here: the frame's GUI update — the pass that puts layout groups and list rows
        // in place — is done, and no sprite batch is being recorded, so a render target of our own is safe to use.
        Guard.Run("Serving the queued menu screenshot failed", () => SandboxMenuWindow.Current?.ServePendingScreenshot());

        if (Screen.Selected != GameMain.GameScreen) { return; }

        Guard.Run("Menu hotkey handling failed", () =>
        {
            if (SpawnLocationPicker.IsActive)
            {
                SpawnLocationPicker.Update();
                return;
            }

            if (DebugConsole.IsOpen || GUI.SettingsMenuOpen || GUI.PauseMenuOpen) { return; }

            // A text box takes the keyboard while it is selected, so the hotkeys stand back; the menu's own keys are
            // left to work it out, as its arrows may still be used next to a box that is being typed into.
            if (GUI.KeyboardDispatcher.Subscriber is null)
            {
                if (Plugin.ToggleKey.IsHit())
                {
                    SandboxMenuWindow.Instance.Toggle();
                    return;
                }

                if (Plugin.GiveKey.IsHit()) { SandboxMenuWindow.Instance.SpawnIntoInventory(); }
            }

            SandboxMenuWindow.Current?.HandleKeys();
        });
    }

    // The host's escape hook: returning true keeps the game from opening its own pause menu. The mod's own game screen
    // runs this before the post-update hooks, so the world position picker is still up here: it is the thing escape
    // closes, and it does that in its own update right after.
    internal static bool HandleEscapeKey()
    {
        if (Screen.Selected != GameMain.GameScreen) { return false; }
        if (DebugConsole.IsOpen || GUI.SettingsMenuOpen || GUI.PauseMenuOpen) { return false; }

        if (SpawnLocationPicker.IsActive) { return true; }

        return SandboxMenuWindow.Current?.HandleEscape() ?? false;
    }
}
