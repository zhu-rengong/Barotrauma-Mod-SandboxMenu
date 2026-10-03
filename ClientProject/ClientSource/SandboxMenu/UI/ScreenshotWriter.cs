using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

// Renders what the menu is showing — the windows it was given — onto an offscreen target and out as a PNG; the
// queue itself stays with the menu, which serves it after the frame's GUI update has run through.
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

        // The host renders at its own virtual resolution and keeps the device's viewport on that, so a render target of
        // the captured area has to be given a viewport of its own: without it the window — which sits in the middle of
        // the canvas — is squeezed into a corner of the image.
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

                // Deferred with the host's own GUI sampler: BackToFront would re-sort the menu's components. The windows
                // are shifted into place themselves (see DrawInto) because parts of the host's GUI restart the batch.
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
            Log.Info($"Saved a screenshot of the menu to '{path}'");
        }
        catch (Exception e)
        {
            Log.Warn("Saving a screenshot of the menu failed", e);
            report(TextManager.Get("sandboxmenu.status.screenshotfailed"));
        }
    }

    // The host draws its pointer in the screen pass, which a screenshot does not take part in, so it is drawn on top
    // of the windows here, in whatever state the menu is showing right now.
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
