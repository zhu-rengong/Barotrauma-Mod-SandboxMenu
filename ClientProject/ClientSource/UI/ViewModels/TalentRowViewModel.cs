namespace SandboxMenu.UI.ViewModels;

internal sealed class TalentRowViewModel(TalentPrefabEntry entry, CharacterInfo? info) : Notifiable
{
    internal TalentPrefabEntry Entry { get; } = entry;

    public TalentDisplay Display { get; } = TalentDisplay.For(entry.Prefab);

    public Sprite? Icon => Display.Icon;

    public Color IconColor => Display.OwnedColor;

    public RichString Title => RichString.Rich(Display.Name + " " + TooltipText.Dim(Display.Identifier));

    public RichString SubText { get; } = SubTextOf(entry);

    public RichString ToolTip { get; } = TalentDisplay.For(entry.Prefab).ToolTip(info, includeApplyHint: false);

    private static RichString SubTextOf(TalentPrefabEntry entry)
    {
        if (entry.SubTrees.Length == 0) { return RichString.Rich(TextManager.Get("sandboxmenu.talents.extra")); }

        List<LocalizedString> labels = [];

        foreach (Identifier subTree in entry.SubTrees) { labels.Add(TalentPrefabCatalog.SubTreeLabel(subTree)); }

        return RichString.Rich(TooltipText.Join(TextManager.Get("sandboxmenu.list.separator"), labels));
    }
}
