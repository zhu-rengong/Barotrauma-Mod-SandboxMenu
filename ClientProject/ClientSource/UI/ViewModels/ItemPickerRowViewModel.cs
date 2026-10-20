namespace SandboxMenu.UI.ViewModels;

internal sealed class ItemPickerRowViewModel : ItemRowViewModel
{
    public ItemPickerRowViewModel(ItemPrefabEntry entry, Action<string> onPicked, Action<string> onSelf, Func<Character?> hintTarget)
        : base(hintTarget)
    {
        Entry = entry;
        Display = ItemDisplay.For(entry.Prefab);
        PickCommand = new RelayCommand(() => onPicked(entry.Identifier));
        SelfCommand = new RelayCommand(() => onSelf(entry.Identifier));
    }

    public ItemPrefabEntry Entry { get; }

    public RelayCommand PickCommand { get; }

    public RelayCommand SelfCommand { get; }

    public RichString TileSelfToolTip => WithSelfHint(TileToolTip);

    internal override void RefreshHints()
    {
        base.RefreshHints();

        Raise(nameof(TileSelfToolTip));
    }

    private static RichString WithSelfHint(RichString text)
    {
        RichString hint = RichString.ColorizeText(TextManager.Get("sandboxmenu.browser.selfhint"), Theme.Callout);

        return RichString.Rich(text.NestedStr + "\n" + hint.NestedStr);
    }
}
