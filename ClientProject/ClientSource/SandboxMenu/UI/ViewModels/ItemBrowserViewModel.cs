namespace SandboxMenu.UI.ViewModels;

internal sealed class ItemPickerRowViewModel : ItemRowViewModel
{
    public ItemPickerRowViewModel(ItemPrefabEntry entry, Action<string> onPicked, Action<string> onSelf)
    {
        Entry = entry;
        Display = ItemDisplay.For(entry.Prefab);
        PickCommand = new RelayCommand(() => onPicked(entry.Identifier));
        SelfCommand = new RelayCommand(() => onSelf(entry.Identifier));
    }

    public ItemPrefabEntry Entry { get; }

    public RelayCommand PickCommand { get; }

    public RelayCommand SelfCommand { get; }

    // The line the browser adds about its buttons, in the call-out colour because it is the one thing to notice.
    public RichString SelfToolTip => WithSelfHint(ToolTip);

    public RichString TileSelfToolTip => WithSelfHint(TileToolTip);

    private static RichString WithSelfHint(RichString text)
    {
        RichString hint = RichString.ColorizeText(TextManager.Get("sandboxmenu.browser.selfhint"), Theme.Callout);

        // Joined as nested markup: joining the two with `+` lands on a LocalizedString and drops every tag.
        return RichString.Rich(text.NestedStr + "\n" + hint.NestedStr);
    }
}

internal sealed class ItemBrowserViewModel : Notifiable
{
    private readonly IDialogHost _host;
    private readonly HashSet<ContentPackage> _packages;
    private readonly List<ItemPickerRowViewModel> _rows;

    private IReadOnlyList<ItemPickerRowViewModel> _visible = [];
    private Action<string> _onPicked = static _ => { };
    private Action<string>? _onSelf;
    private string _query = string.Empty;
    private MapEntityCategory _categories;
    private float _scroll;

    private ContainerRules? _container;
    private string? _parent;
    private bool _containerOnly;
    private bool _hideHidden = true;
    private HashSet<ItemPrefab> _fits = new(ReferenceEqualityComparer.Instance);

    public ItemBrowserViewModel(IDialogHost host)
    {
        _host = host;

        _packages = new HashSet<ContentPackage>(ReferenceEqualityComparer.Instance);

        PickPackagesCommand = new RelayCommand(PickPackages);
        PickCategoriesCommand = new RelayCommand(PickCategories);

        _rows = [.. ItemPrefabCatalog.All().Select(entry => new ItemPickerRowViewModel(entry, Picked, UseOnSelf))];

        ApplyFilter();
    }

    internal void PickInto(Action<string> onPicked) => _onPicked = onPicked;

    // What the right mouse button does with a picked item, when whoever opened the browser has somewhere to put it.
    internal void UseOnSelf(Action<string>? onSelf) => _onSelf = onSelf;

    private void UseOnSelf(string identifier) => _onSelf?.Invoke(identifier);

    // The list's scroll position, bound two-way: writing it is how the list is sent back to the top, and the list
    // reports the player's scrolling back through it.
    public float Scroll
    {
        get => _scroll;
        set => Set(ref _scroll, value);
    }

    internal void RefreshHints()
    {
        foreach (ItemPickerRowViewModel row in _visible) { row.RefreshHints(); }
    }

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

    public LocalizedString HiddenFilterLabel => TextManager.Get("sandboxmenu.filter.hide.hidden");

    // Ticked by the tick box in the filter row, and ticked from the start: what the menus themselves leave out stays
    // out of the list until the tick is taken off.
    public bool HideHidden
    {
        get => _hideHidden;
        set
        {
            if (_hideHidden == value) { return; }

            _hideHidden = value;
            ApplyFilter();
        }
    }

    public RelayCommand PickPackagesCommand { get; }

    public RelayCommand PickCategoriesCommand { get; }

    public IReadOnlyList<ItemPickerRowViewModel> Rows => _visible;

    // Picking the item of an entry inside another item: what the parent's container accepts decides the list. The same
    // parent hands back the same rows and keeps the scroll; another parent is another list and starts at the top.
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
        => (!_containerOnly || _fits.Contains(entry.Prefab))
            && (!_hideHidden || !entry.Prefab.HideInMenus)
            && (query.Length == 0 || entry.Matches(query))
            && filter.Allows(entry);

    private void RefreshFits()
    {
        HashSet<ItemPrefab> fits = new(ReferenceEqualityComparer.Instance);

        if (_containerOnly && _container is { } container)
        {
            foreach (ItemPrefabEntry entry in ItemPrefabCatalog.All())
            {
                if (container.Allows(entry.Prefab)) { fits.Add(entry.Prefab); }
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
                RichString.Rich(Labels.AccentMarkup(package.Label, package.Package)),
                () => _packages.Contains(package.Package),
                selected => SetPackage(package.Package, selected))));

    private void PickCategories()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.filter.categories"),
            ItemPrefabCatalog.CategoriesIn(_packages).Select(category => new PickerToggle(
                CategoryName(category),
                () => _categories.HasFlag(category),
                selected => SetCategory(category, selected),
                CategoryIcon(category))));

    private static LocalizedString CategoryName(MapEntityCategory category)
    {
        LocalizedString name = TextManager.Get($"MapEntityCategory.{category}");

        return string.IsNullOrEmpty(name.Value) ? category.ToString() : name;
    }

    // The icon is the one the game's own category buttons wear, so a category reads here the way it reads in the
    // fabricator; a category the game has no button style for simply has no icon.
    private static Sprite? CategoryIcon(MapEntityCategory category)
        => GUIStyle.GetComponentStyle(new Identifier("CategoryButton." + category))
            is { } style && style.Sprites.TryGetValue(GUIComponent.ComponentState.None, out List<UISprite>? sprites)
            && sprites.Count > 0
                ? sprites[0].Sprite
                : null;

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

    // A filter the player changed starts the list at the top; reopening the browser hands it the same rows again and
    // leaves the scroll alone.
    private void ApplyFilter(bool keepScroll = false)
    {
        ItemFilter filter = new(_packages, _categories);
        string query = _query.Trim();

        _visible = [.. _rows.Where(row => Shows(row.Entry, filter, query))];

        // Raised even when it does not change: the list has to be told to go back to the top, not read.
        if (!keepScroll)
        {
            _scroll = 0f;
            Raise(nameof(Scroll));
        }

        Raise(nameof(Rows));

        Raise(nameof(PackageFilter));
        Raise(nameof(CategoryFilter));
    }
}
