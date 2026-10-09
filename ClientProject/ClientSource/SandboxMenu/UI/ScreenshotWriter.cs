using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

internal static class ScreenshotWriter
{
    internal static void Write(IReadOnlyList<IDialogWindow> drawn, Action<LocalizedString> report)
    {
        Rectangle area = drawn[0].Rect;
        foreach (IDialogWindow popup in drawn) { area = Rectangle.Union(area, popup.Rect); }

        if (area.Width <= 0 || area.Height <= 0) { return; }

        string folder = Path.Combine(Plugin.SettingsService.SaveFolder, "Screenshots");
        string name = $"sandboxmenu {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png";
        string path = Path.Combine(folder, name);

        GraphicsDevice device = GameMain.Instance.GraphicsDevice;

        Viewport previousViewport = device.Viewport;
        Rectangle previousScissor = device.ScissorRectangle;

        try
        {
            Directory.CreateDirectory(folder);

            using RenderTarget2D target = new(device, area.Width, area.Height, false, SurfaceFormat.Color, DepthFormat.None);
            using SpriteBatch batch = new(device);

            try
            {
                device.SetRenderTarget(target);
                device.Viewport = new Viewport(0, 0, target.Width, target.Height);
                device.ScissorRectangle = new Rectangle(0, 0, target.Width, target.Height);
                device.Clear(Color.Transparent);

                batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, GUI.SamplerState, null, null, null);

                try
                {
                    Point shift = new(-area.X, -area.Y);

                    foreach (IDialogWindow popup in drawn) { popup.DrawInto(batch, shift); }

                    DrawCursor(batch, shift);
                }
                finally
                {
                    batch.End();
                }

                device.SetRenderTarget(null);

                using FileStream stream = File.Create(path);
                target.SaveAsPng(stream, target.Width, target.Height);
            }
            finally
            {
                device.SetRenderTarget(null);
                device.Viewport = previousViewport;
                device.ScissorRectangle = previousScissor;
                GameMain.Instance.ResetViewPort();
            }

            report(TextManager.GetWithVariable("sandboxmenu.status.screenshot", "[name]", name));
            DebugConsole.NewMessage($"Saved a screenshot of the menu to '{path}'");
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Saving a screenshot of the menu failed: {e}");
            report(TextManager.Get("sandboxmenu.status.screenshotfailed"));
        }
    }

    private static void DrawCursor(SpriteBatch spriteBatch, Point shift)
    {
        if (!GameMain.WindowActive || GUI.HideCursor || !GUI.MouseCursorSprites.Prefabs.Any()) { return; }

        Sprite? sprite = GUI.MouseCursorSprites[GUI.MouseCursor] ?? GUI.MouseCursorSprites[CursorState.Default];

        if (sprite is null) { return; }

        sprite.Draw(
            spriteBatch,
            PlayerInput.LatestMousePosition + shift.ToVector2(),
            Color.White,
            sprite.Origin,
            0f,
            GUI.Scale / 1.5f,
            SpriteEffects.None,
            null);
    }
}
