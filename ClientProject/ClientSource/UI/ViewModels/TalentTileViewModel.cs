namespace SandboxMenu.UI.ViewModels;

internal sealed class TalentTileViewModel : Notifiable
{
    private bool _owned;

    internal TalentTileViewModel(TalentPrefabEntry entry, CharacterInfo? info, bool owned, Action<TalentTileViewModel> onPicked)
    {
        Entry = entry;
        Display = TalentDisplay.For(entry.Prefab);
        ToolTip = Display.ToolTip(info);
        _owned = owned;
        PickCommand = new RelayCommand(() => onPicked(this));
    }

    internal TalentPrefabEntry Entry { get; }

    public TalentDisplay Display { get; }

    public Sprite? Icon => Display.Icon;

    public Color IconColor => _owned ? Display.OwnedColor : Color.White;

    public RichString Name => RichString.Rich(Display.Name);

    public RichString ToolTip { get; }

    public RelayCommand PickCommand { get; }

    internal TalentPrefab Prefab => Entry.Prefab;

    internal void SetOwned(bool owned)
    {
        if (_owned == owned) { return; }

        _owned = owned;

        Raise(nameof(IconColor));
    }
}
