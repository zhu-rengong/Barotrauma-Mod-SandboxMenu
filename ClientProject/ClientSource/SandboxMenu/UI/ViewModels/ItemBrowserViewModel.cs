using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

public sealed class ItemPickerRowViewModel : ItemRowViewModel
{
    public ItemPickerRowViewModel(ItemPrefabEntry entry, Action<string> onPicked)
    {
        Entry = entry;
        Display = entry.Display;
        PickCommand = new RelayCommand(() => onPicked(entry.Display.Identifier));
    }

    public ItemPrefabEntry Entry { get; }

    public RelayCommand PickCommand { get; }
}

public sealed class ItemBrowserViewModel : Notifiable
{
    private readonly IDialogHost _host;
    private readonly HashSet<string> _packages;
    private readonly IReadOnlyList<ItemPrefabEntry> _entries;

    private Action<string> _onPicked = static _ => { };
    private string _query = string.Empty;
    private MapEntityCategory _categories;
    private GUIListBox? _results;

    public ItemBrowserViewModel(IDialogHost host)
    {
        _host = host;

        // Package names are identifiers, and the catalog offers them ignoring case: the set a tick is kept in has
        // to compare them the same way, or a tick would not stick to the spelling the catalog reports.
        _packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        PickPackagesCommand = new RelayCommand(PickPackages);
        PickCategoriesCommand = new RelayCommand(PickCategories);

        _entries = ItemPrefabCatalog.All();

        // The list is one row per loaded item, and what it holds never changes again: the query and the filters
        // show and hide the rows that are already there instead of handing the list a different set of them.
        FillAll();
    }

    internal void Attach(GUIListBox? results) => _results = results;

    internal void PickInto(Action<string> onPicked) => _onPicked = onPicked;

    public string Query
    {
        get => _query;
        set
        {
            if (Set(ref _query, value ?? string.Empty)) { ApplyFilter(); }
        }
    }

    public LocalizedString PackageFilter => FilterLabel("sandboxmenu.filter.packages", _packages.Count);

    public LocalizedString CategoryFilter => FilterLabel("sandboxmenu.filter.categories", CategoryCount);

    public RelayCommand PickPackagesCommand { get; }

    public RelayCommand PickCategoriesCommand { get; }

    public ObservableCollection<ItemPickerRowViewModel> Rows { get; } = [];

    private void FillAll()
    {
        ItemFilter filter = new(_packages, _categories);
        string query = _query.Trim();

        foreach (ItemPrefabEntry entry in _entries)
        {
            ItemPickerRowViewModel row = new(entry, Picked);

            row.Visible = Shows(row.Entry, filter, query);

            Rows.Add(row);
        }
    }

    private void Picked(string identifier) => _onPicked(identifier);

    private static bool Shows(ItemPrefabEntry entry, ItemFilter filter, string query)
        => (query.Length == 0 || entry.Matches(query)) && filter.Allows(entry);

    private int CategoryCount
    {
        get
        {
            int count = 0;

            foreach (MapEntityCategory category in ItemPrefabCatalog.AllCategories)
            {
                if ((_categories & category) != 0) { count++; }
            }

            return count;
        }
    }

    private static LocalizedString FilterLabel(string nameKey, int chosen)
    {
        LocalizedString count = chosen.ToString();

        return TextManager.GetWithVariables(
            chosen == 0 ? "sandboxmenu.filter.summary.all" : "sandboxmenu.filter.summary.some",
            ("[name]", TextManager.Get(nameKey)),
            ("[count]", count));
    }

    private void PickPackages()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.filter.packages"),
            ItemPrefabCatalog.Packages().Select(name => new PickerToggle(
                name,
                () => _packages.Contains(name),
                selected => SetPackage(name, selected))));

    private void PickCategories()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.filter.categories"),
            ItemPrefabCatalog.Categories().Select(category => new PickerToggle(
                CategoryName(category),
                () => _categories.HasFlag(category),
                selected => SetCategory(category, selected))));

    private static LocalizedString CategoryName(MapEntityCategory category)
    {
        LocalizedString name = TextManager.Get($"MapEntityCategory.{category}");

        return string.IsNullOrEmpty(name.Value) ? category.ToString() : name;
    }

    private void SetPackage(string name, bool selected)
    {
        if (selected) { _packages.Add(name); }
        else { _packages.Remove(name); }

        ApplyFilter();
    }

    private void SetCategory(MapEntityCategory category, bool selected)
    {
        _categories = selected ? _categories | category : _categories & ~category;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        ItemFilter filter = new(_packages, _categories);
        string query = _query.Trim();

        for (int i = 0; i < Rows.Count; i++)
        {
            ItemPickerRowViewModel row = Rows[i];

            row.Visible = Shows(row.Entry, filter, query);
        }

        if (_results is { } list) { list.BarScroll = 0f; }

        Raise(nameof(PackageFilter));
        Raise(nameof(CategoryFilter));
    }
}
