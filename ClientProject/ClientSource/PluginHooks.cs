using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu;

[RunConstructorOnInit]
internal static class PluginHooks
{
    static PluginHooks()
    {
        Services.HookService.RegisterHook<PluginGameModeAddToGUIUpdateListDelegate>(AddToGUIUpdateList);
        Services.HookService.RegisterHook<PluginGameModeDrawDelegate>(GameModeDraw);
        Services.HookService.RegisterHook<PluginOnPostUpdateDelegate>(PostUpdate);
        Services.HookService.RegisterHook<PluginHandleEscapeKeyDelegate>(HandleEscapeKey);
    }

    internal static void AddToGUIUpdateList(GameMode mode)
    {
        try
        {
            if (Screen.Selected != GameMain.GameScreen) { return; }
            if (!SandboxMenuHost.Instance.IsOpen) { return; }

            SandboxMenuHost.Instance.AddToUpdateList();
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Updating the sandbox menu failed: {e}");
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
            Services.DebugConsole.AddWarning($"Drawing the spawn point hint failed: {e}");
        }
    }

    internal static void PostUpdate(float deltaTime)
    {
        try
        {
            UiHost.AdvanceFrame(deltaTime);

            CharacterPreview.RenderPending();
            SandboxMenuHost.Instance.ServePendingScreenshot();

            if (Screen.Selected != GameMain.GameScreen) { return; }

            HandleHotkeys();
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"The sandbox menu's post-update failed: {e}");
        }
    }

    internal static bool HandleEscapeKey()
    {
        if (Screen.Selected != GameMain.GameScreen) { return false; }
        if (DebugConsole.IsOpen || GUI.SettingsMenuOpen || GUI.PauseMenuOpen) { return false; }

        if (SpawnPointPicker.IsActive) { return true; }

        return SandboxMenuHost.Instance.HandleEscape();
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
            if (KeyBinds.Toggle.IsHit())
            {
                SandboxMenuHost.Instance.Toggle();
                return;
            }

            if (KeyBinds.Give.IsHit()) { SandboxMenuHost.Instance.SpawnIntoInventory(); }
        }

        SandboxMenuHost.Instance.HandleKeys();
    }
}
