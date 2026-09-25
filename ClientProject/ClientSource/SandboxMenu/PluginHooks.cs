using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu;

internal static class PluginHooks
{
    // One delegate instance per hook, held here rather than passed as a method group at each call site: the game
    // finds a hook to take back by the delegate it was given, and two conversions of the same method group are
    internal static readonly PluginGameModeAddToGUIUpdateListDelegate AddToGUIUpdateListHook = AddToGUIUpdateList;

    internal static readonly PluginGameModeDrawDelegate GameModeDrawHook = GameModeDraw;

    internal static readonly PluginOnPostUpdateDelegate PostUpdateHook = PostUpdate;

    internal static void AddToGUIUpdateList(GameMode mode)
    {
        if (Screen.Selected != GameMain.GameScreen) { return; }

        // Asked through Current rather than Instance: Instance builds the shell on demand, and a hook that runs
        // every frame would put one back together the moment the mod was taken apart.
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
        if (Screen.Selected != GameMain.GameScreen) { return; }

        Guard.Run("Menu hotkey handling failed", () =>
        {
            if (SpawnLocationPicker.IsActive)
            {
                SpawnLocationPicker.Update();
                return;
            }

            if (DebugConsole.IsOpen || GUI.KeyboardDispatcher.Subscriber is not null) { return; }
            if (GUI.SettingsMenuOpen || GUI.PauseMenuOpen) { return; }

            if (Plugin.ToggleKey.IsHit())
            {
                SandboxMenuWindow.Instance.Toggle();
            }
        });
    }
}
