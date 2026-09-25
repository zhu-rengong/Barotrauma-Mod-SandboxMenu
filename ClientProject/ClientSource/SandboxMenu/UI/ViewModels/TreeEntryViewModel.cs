namespace SandboxMenu.UI.ViewModels;

public sealed class TreeEntryViewModel : ItemRowViewModel
{
    private bool _isSelected;

    internal TreeEntryViewModel(SpawnMenuViewModel menu, SpawnEntry entry, List<SpawnEntry> owner, int depth)
    {
        Menu = menu;
        Entry = entry;
        Owner = owner;
        Indent = (int)(MenuTheme.TreeIndentStart + depth * MenuTheme.TreeIndentStep);

        SelectCommand = new RelayCommand(() => menu.Select(this));
        MenuCommand = new RelayCommand(() => menu.OpenMenu(this));

        Refresh();
    }

    public SpawnMenuViewModel Menu { get; }

    public SpawnEntry Entry { get; }

    public List<SpawnEntry> Owner { get; }

    public int Indent { get; }

    public override RichString Title
        => Display is { } display && Entry is ItemEntry item
            ? display.TitleFor(display.Name + item.AmountSummary)
            : RichString.Rich(Entry.Summary);

    public string ColorName => Entry switch
    {
        RefEntry => "Dim",
        _ => "Bright"
    };

    public bool IsSelected
    {
        get => _isSelected;
        internal set => Set(ref _isSelected, value);
    }

    public RelayCommand SelectCommand { get; }

    public RelayCommand MenuCommand { get; }

    internal void Refresh()
    {
        Display = Entry is ItemEntry item ? ItemDisplay.For(item.Identifier) : null;

        Raise(nameof(Title));
        Raise(nameof(ColorName));
    }
}
