namespace SandboxMenu.UI.ViewModels;

internal sealed class TreeEntryViewModel : ItemRowViewModel
{
    private bool _isSelected;

    internal TreeEntryViewModel(
        SpawnEntry entry,
        List<SpawnEntry> owner,
        int depth,
        ItemEntry? container,
        Action<TreeEntryViewModel> select,
        Action<TreeEntryViewModel> showContextMenu,
        Func<Character?> hintTarget)
        : base(hintTarget)
    {
        Entry = entry;
        Owner = owner;
        Container = container;
        Depth = depth;

        SelectCommand = new RelayCommand(() => select(this));
        OpenMenuCommand = new RelayCommand(() => showContextMenu(this));

        Refresh();
    }

    public SpawnEntry Entry { get; }

    public List<SpawnEntry> Owner { get; }

    internal ItemEntry? Container { get; }

    public int Depth { get; }

    public override RichString Title
        => Display is { } display && Entry is ItemEntry item
            ? display.TitleFor(display.Name + EntrySummary.AmountSuffix(item))
            : RichString.Rich(EntrySummary.Text(Entry));

    public bool IsReference => Entry is ReferenceEntry;

    public bool IsSelected
    {
        get => _isSelected;
        internal set => Set(ref _isSelected, value);
    }

    public RelayCommand SelectCommand { get; }

    public RelayCommand OpenMenuCommand { get; }

    internal void Refresh()
    {
        ItemDisplay? display = Entry is ItemEntry item ? ItemDisplay.For(item.Identifier) : null;
        bool unchanged = ReferenceEquals(Display, display);

        Display = display;

        if (unchanged) { Raise(nameof(Title)); }

        Raise(nameof(IsReference));
    }
}
