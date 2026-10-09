namespace SandboxMenu.UI.ViewModels;

internal sealed class TreeEntryViewModel : ItemRowViewModel
{
    private bool _isSelected;

    internal TreeEntryViewModel(SpawnPanelViewModel menu, SpawnEntry entry, List<SpawnEntry> owner, int depth, ItemEntry? container)
    {
        Menu = menu;
        Entry = entry;
        Owner = owner;
        Container = container;
        Depth = depth;

        SelectCommand = new RelayCommand(() => menu.Select(this));
        OpenMenuCommand = new RelayCommand(() => menu.OpenMenu(this));

        Refresh();
    }

    public SpawnPanelViewModel Menu { get; }

    public SpawnEntry Entry { get; }

    public List<SpawnEntry> Owner { get; }

    internal ItemEntry? Container { get; }

    public int Depth { get; }

    public override RichString Title
        => Display is { } display && Entry is ItemEntry item
            ? display.TitleFor(display.Name + EntrySummary.AmountSuffix(item))
            : RichString.Rich(EntrySummary.Text(Entry));

    public string ColorName => Entry switch
    {
        ReferenceEntry => "Dim",
        _ => "Bright"
    };

    public bool IsSelected
    {
        get => _isSelected;
        internal set => Set(ref _isSelected, value);
    }

    public RelayCommand SelectCommand { get; }

    public RelayCommand OpenMenuCommand { get; }

    internal void Refresh()
    {
        Display = Entry is ItemEntry item ? ItemDisplay.For(item.Identifier) : null;

        Raise(nameof(Title));
        Raise(nameof(ColorName));
    }
}
