namespace SandboxMenu.UI.ViewModels;

internal sealed class ItemPickerRowViewModel : ItemRowViewModel
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

internal sealed class ItemBrowserViewModel : Notifiable
{
    private readonly IDialogHost _host;
    private readonly HashSet<ContentPackage> _packages;
    private readonly List<ItemPickerRowViewModel> _rows;

    private IReadOnlyList<ItemPickerRowViewModel> _visible = [];
    private Action<string> _onPicked = static _ => { };
    private string _query = string.Empty;
    private MapEntityCategory _categories;
    private GUIListBox? _results;

    private ContainerRules? _container;
    private string? _parent;
    private bool _containerOnly;
    private HashSet<ItemPrefab> _fits = new(ReferenceEqualityComparer.Instance);

    public ItemBrowserViewModel(IDialogHost host)
    {
        _host = host;

        _packages = new HashSet<ContentPackage>(ReferenceEqualityComparer.Instance);

        PickPackagesCommand = new RelayCommand(PickPackages);
        PickCategoriesCommand = new RelayCommand(PickCategories);

        _rows = [.. ItemPrefabCatalog.All().Select(entry => new ItemPickerRowViewModel(entry, Picked))];

        ApplyFilter();
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

    public bool ContainerFilterVisible => _container is not null;

    public LocalizedString ContainerFilterLabel => TextManager.Get("sandboxmenu.filter.container.only");

    // Ticked by the tick box in the filter row: only the items the parent container takes.
    public bool ContainerOnly
    {
        get => _containerOnly;
        set
        {
            if (_containerOnly == value) { return; }

            _containerOnly = value;

            RefreshFits();
            ApplyFilter();
        }
    }

    public RelayCommand PickPackagesCommand { get; }

    public RelayCommand PickCategoriesCommand { get; }

    public IReadOnlyList<ItemPickerRowViewModel> Rows => _visible;

    // The browser also picks the item of an entry that sits inside another item: what the parent's container accepts
    // (its containable rules) then decides the list, and the tick box starts out ticked. Opening it again for the
    // parent it was last opened for hands back the same rows and keeps the place the list was left at; another
    // parent is another list and starts at the top.
    internal void UseParent(ItemEntry? parent)
    {
        bool sameList = string.Equals(_parent, parent?.Identifier, StringComparison.OrdinalIgnoreCase);

        _parent = parent?.Identifier;
        _container = ContainerRules.For(parent?.Identifier);
        _containerOnly = _container is not null;

        RefreshFits();

        Raise(nameof(ContainerFilterVisible));
        Raise(nameof(ContainerOnly));

        ApplyFilter(keepScroll: sameList);
    }

    private void Picked(string identifier) => _onPicked(identifier);

    private bool Shows(ItemPrefabEntry entry, ItemFilter filter, string query)
        => (!_containerOnly || _fits.Contains(entry.Display.Prefab))
            && (query.Length == 0 || entry.Matches(query))
            && filter.Allows(entry);

    private void RefreshFits()
    {
        HashSet<ItemPrefab> fits = new(ReferenceEqualityComparer.Instance);

        if (_containerOnly && _container is { } container)
        {
            foreach (ItemPrefabEntry entry in ItemPrefabCatalog.All())
            {
                if (container.Allows(entry.Display.Prefab)) { fits.Add(entry.Display.Prefab); }
            }
        }

        _fits = fits;
    }


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
            ItemPrefabCatalog.Packages().Select(package => new PickerToggle(
                package.Label,
                () => _packages.Contains(package.Package),
                selected => SetPackage(package.Package, selected))));

    private void PickCategories()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.filter.categories"),
            ItemPrefabCatalog.CategoriesIn(_packages).Select(category => new PickerToggle(
                CategoryName(category),
                () => _categories.HasFlag(category),
                selected => SetCategory(category, selected))));

    private static LocalizedString CategoryName(MapEntityCategory category)
    {
        LocalizedString name = TextManager.Get($"MapEntityCategory.{category}");

        return string.IsNullOrEmpty(name.Value) ? category.ToString() : name;
    }

    private void SetPackage(ContentPackage package, bool selected)
    {
        if (selected) { _packages.Add(package); }
        else { _packages.Remove(package); }

        _categories &= ItemPrefabCatalog.CategoryMaskIn(_packages);

        ApplyFilter();
    }

    private void SetCategory(MapEntityCategory category, bool selected)
    {
        _categories = selected ? _categories | category : _categories & ~category;

        ApplyFilter();
    }

    // A filter the player has just changed starts the list at the top; reopening the browser is handed the same
    // rows again and leaves the place the list was left at, so it must not reset the scroll.
    private void ApplyFilter(bool keepScroll = false)
    {
        ItemFilter filter = new(_packages, _categories);
        string query = _query.Trim();

        _visible = [.. _rows.Where(row => Shows(row.Entry, filter, query))];

        if (!keepScroll && _results is { } list) { list.BarScroll = 0f; }

        Raise(nameof(Rows));

        Raise(nameof(PackageFilter));
        Raise(nameof(CategoryFilter));
    }
}
