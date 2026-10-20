using System.Xml.Linq;
using Microsoft.Xna.Framework.Input;

namespace SandboxMenu.Domain.Persistence;

internal sealed class PresetFiles(
    Action<StatusKind, LocalizedString> report,
    IDialogHost host,
    SpawnSet set,
    Action<SpawnSet> reloadSet)
{
    internal void Save()
    {
        string name = set.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.nopresetname"));
            return;
        }

        if (TemplateStore.ReachesTemplate(set, name))
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.templatecycle"));
            return;
        }

        bool saved = TemplateStore.Save(set);

        report(
            saved ? StatusKind.Success : StatusKind.Failure,
            saved
                ? TextManager.Get("sandboxmenu.status.presetsaved")
                : TextManager.GetWithVariable("sandboxmenu.status.presetsavefailed", "[name]", name));
    }

    internal void Load()
    {
        IReadOnlyList<string> presets = TemplateStore.ListPresets();

        if (presets.Count == 0)
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.nopresets"));
            return;
        }

        host.ShowOptions(
            TextManager.Get("sandboxmenu.preset.load"),
            presets.Select(name => new PickerOption(name, () => Load(name))));
    }

    internal void Reload()
    {
        string name = set.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.nopresetname"));
            return;
        }

        if (Apply(name))
        {
            report(StatusKind.Success, TextManager.GetWithVariable("sandboxmenu.status.presetreloaded", "[name]", name));
        }
    }

    internal void Open()
    {
        string name = set.Name;
        bool named = !string.IsNullOrWhiteSpace(name);

        host.ShowPresetEditor(new PresetEditorRequest(
            named
                ? TextManager.GetWithVariable("sandboxmenu.preset.editor.title", "[name]", name)
                : TextManager.Get("sandboxmenu.preset.open"),
            named ? Read(name) : CurrentText(),
            () => named ? Read(name) : CurrentText(),
            text => SaveText(name, text),
            CopyPath));
    }

    private string Read(string name)
    {
        if (TemplateStore.ReadText(name) is { } text) { return text; }

        if (TemplateStore.Exists(name))
        {
            report(StatusKind.Warning, TextManager.GetWithVariable("sandboxmenu.status.presetreadfailed", "[name]", name));
        }

        return CurrentText();
    }

    private string? SaveText(string name, string text)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.nopresetname"));
            return null;
        }

        if (!TemplateStore.TryParse(name, text, out SpawnSet? parsed) || parsed is null)
        {
            report(StatusKind.Failure, TextManager.Get("sandboxmenu.status.presettextinvalid"));
            return null;
        }

        if (TemplateStore.ReachesTemplate(parsed, name))
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.templatecycle"));
            return null;
        }

        if (!TemplateStore.WriteText(name, text))
        {
            report(StatusKind.Failure, TextManager.GetWithVariable("sandboxmenu.status.presetsavefailed", "[name]", name));
            return null;
        }

        if (!Apply(name)) { return null; }

        report(StatusKind.Success, TextManager.GetWithVariable("sandboxmenu.status.presettextsaved", "[name]", name));

        return TemplateStore.ReadText(name) ?? TemplateStore.Normalize(text);
    }

    private string CurrentText() => TemplateStore.Normalize(new XDocument(set.ToXml()).ToString());

    private void CopyPath()
    {
        string name = set.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.nopresetname"));
            return;
        }

        if (!TemplateStore.Exists(name))
        {
            report(StatusKind.Warning, TextManager.GetWithVariable("sandboxmenu.status.presetnotfound", "[name]", name));
            return;
        }

        string path = TemplateStore.PathOf(name);

        try
        {
            Clipboard.SetText(path);
            report(StatusKind.Success, TextManager.GetWithVariable("sandboxmenu.status.presetpathcopied", "[name]", name));
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Failed to copy the preset path '{path}': {e}");
            report(StatusKind.Failure, TextManager.GetWithVariable("sandboxmenu.status.presetpathcopyfailed", "[name]", name));
        }
    }

    private void Load(string name)
    {
        if (Apply(name))
        {
            report(StatusKind.Success, TextManager.GetWithVariable("sandboxmenu.status.presetloaded", "[name]", name));
        }
    }

    private bool Apply(string name)
    {
        if (TemplateStore.TryLoad(name, out SpawnSet? loaded) && loaded is not null)
        {
            loaded.Name = name;
            reloadSet(loaded);
            return true;
        }

        report(StatusKind.Failure, TextManager.GetWithVariable("sandboxmenu.status.presetloadfailed", "[name]", name));
        return false;
    }
}
