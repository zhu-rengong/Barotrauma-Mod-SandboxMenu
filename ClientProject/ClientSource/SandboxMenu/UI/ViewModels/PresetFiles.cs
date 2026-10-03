using System.Diagnostics;

namespace SandboxMenu.UI.ViewModels;

// The preset commands behind the toolbar: save what the menu holds, list the stored presets, reload one from disk
// or open its file. Every outcome is reported through the menu's status line.
internal sealed class PresetFiles(SpawnMenuViewModel menu, SpawnSet set)
{
    internal void Save()
    {
        string name = set.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            menu.Report(TextManager.Get("sandboxmenu.status.nopresetname"));
            return;
        }

        if (TemplateStore.ReachesTemplate(set, name))
        {
            menu.Report(TextManager.Get("sandboxmenu.status.templatecycle"));
            return;
        }

        menu.Report(TemplateStore.Save(set)
            ? TextManager.Get("sandboxmenu.status.presetsaved")
            : TextManager.GetWithVariable("sandboxmenu.status.presetsavefailed", "[name]", name));
    }

    internal void Load()
    {
        IReadOnlyList<string> presets = TemplateStore.ListPresets();

        if (presets.Count == 0)
        {
            menu.Report(TextManager.Get("sandboxmenu.status.nopresets"));
            return;
        }

        menu.Host.ShowOptions(
            TextManager.Get("sandboxmenu.preset.load"),
            presets.Select(name => new PickerOption(name, () => Load(name))));
    }

    internal void Reload()
    {
        string name = set.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            menu.Report(TextManager.Get("sandboxmenu.status.nopresetname"));
            return;
        }

        if (Apply(name))
        {
            menu.Report(TextManager.GetWithVariable("sandboxmenu.status.presetreloaded", "[name]", name));
        }
    }

    internal void Open()
    {
        string name = set.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            menu.Report(TextManager.Get("sandboxmenu.status.nopresetname"));
            return;
        }

        if (!TemplateStore.Exists(name))
        {
            menu.Report(TextManager.GetWithVariable("sandboxmenu.status.presetnotfound", "[name]", name));
            return;
        }

        string path = TemplateStore.PathOf(name);

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            menu.Report(TextManager.GetWithVariable("sandboxmenu.status.presetopened", "[name]", name));
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to open the preset file '{path}'", e);
            menu.Report(TextManager.GetWithVariable("sandboxmenu.status.presetopenfailed", "[name]", name));
        }
    }

    private void Load(string name)
    {
        if (Apply(name))
        {
            menu.Report(TextManager.GetWithVariable("sandboxmenu.status.presetloaded", "[name]", name));
        }
    }

    private bool Apply(string name)
    {
        if (TemplateStore.TryLoad(name, out SpawnSet? loaded) && loaded is not null)
        {
            loaded.Name = name;
            menu.ReloadSet(loaded);
            return true;
        }

        menu.Report(TextManager.GetWithVariable("sandboxmenu.status.presetloadfailed", "[name]", name));
        return false;
    }
}
