namespace SandboxMenu.UI.ViewModels;

internal sealed class ItemBrowserViewModel : Notifiable
{
    private readonly IDialogHost _host;
    private readonly Func<Character?> _hintTarget;
    private readonly HashSet<string> _packages;
    private readonly FilteredCatalog<ItemPrefabEntry, ItemPickerRowViewModel> _catalog;

    private IReadOnlyList<ItemPickerRowViewModel> _visible = [];
    private Action<string> _onPicked = static _ => { };
    private Action<string>? _onSelf;
    private string _query = string.Empty;
    private MapEntityCategory _categories;
    private float _scroll;
    private bool _keepScroll;

    private ContainerRules? _container;
    private string? _parent;
    private bool _containerOnly;
    private bool _hideHidden = true;
    private HashSet<ItemPrefab> _fits = new(ReferenceEqualityComparer.Instance);
    private ContainerRules? _fitsRules;

    public ItemBrowserViewModel(IDialogHost host, Func<Character?> hintTarget)
    {
        _host = host;
        _hintTarget = hintTarget;

        _packages = new HashSet<string>(PackageLabels.KeyComparer);

        PickPackagesCommand = new RelayCommand(PickPackages);
        PickCategoriesCommand = new RelayCommand(PickCategories);

        _catalog = new FilteredCatalog<ItemPrefabEntry, ItemPickerRowViewModel>([.. ItemPrefabCatalog.All()]);

        Refresh();
    }

    internal void PickInto(Action<string> onPicked) => _onPicked = onPicked;

    internal void UseOnSelf(Action<string>? onSelf) => _onSelf = onSelf;

    private void UseOnSelf(string identifier) => _onSelf?.Invoke(identifier);

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
            if (Set(ref _query, value ?? string.Empty)) { Refresh(); }
        }
    }

    public LocalizedString PackageFilter => FilterSummary.Label("sandboxmenu.filter.packages", _packages.Count);

    public LocalizedString CategoryFilter => FilterSummary.Label("sandboxmenu.filter.categories", CategoryCount);

    public bool ContainerFilterVisible => _container is not null;

    public LocalizedString ContainerFilterLabel => TextManager.Get("sandboxmenu.filter.container.only");

    public bool ContainerOnly
    {
        get => _containerOnly;
        set
        {
            if (_containerOnly == value) { return; }

            _containerOnly = value;

            RebuildFits();
            Refresh();
        }
    }

    public LocalizedString HiddenFilterLabel => TextManager.Get("sandboxmenu.filter.hide.hidden");

    public bool HideHidden
    {
        get => _hideHidden;
        set
        {
            if (_hideHidden == value) { return; }

            _hideHidden = value;
            Refresh();
        }
    }

    public RelayCommand PickPackagesCommand { get; }

    public RelayCommand PickCategoriesCommand { get; }

    public IReadOnlyList<ItemPickerRowViewModel> Rows => _visible;

    internal void UseParent(ItemEntry? parent)
    {
        bool sameList = string.Equals(_parent, parent?.Identifier, StringComparison.OrdinalIgnoreCase);

        _parent = parent?.Identifier;
        _container = ContainerRules.For(parent?.Identifier);
        _containerOnly = _container is not null;

        RebuildFits();

        Raise(nameof(ContainerFilterVisible));
        Raise(nameof(ContainerOnly));

        Refresh(keepScroll: sameList);
    }

    private void Picked(string identifier) => _onPicked(identifier);

    private bool Shows(ItemPrefabEntry entry, ItemFilter filter, string query)
        => (!_containerOnly || _fits.Contains(entry.Prefab))
            && (!_hideHidden || !entry.Prefab.HideInMenus)
            && (query.Length == 0 || entry.Matches(query))
            && filter.Allows(entry);

    private void RebuildFits()
    {
        if (_containerOnly && _container is { } container)
        {
            if (ReferenceEquals(_fitsRules, container)) { return; }

            HashSet<ItemPrefab> fits = new(ReferenceEqualityComparer.Instance);

            foreach (ItemPrefabEntry entry in ItemPrefabCatalog.All())
            {
                if (container.Allows(entry.Prefab)) { fits.Add(entry.Prefab); }
            }

            _fits = fits;
            _fitsRules = container;

            return;
        }

        _fits = new HashSet<ItemPrefab>(ReferenceEqualityComparer.Instance);
        _fitsRules = null;
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

    private void PickPackages()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.filter.packages"),
            ItemPrefabCatalog.Packages().Select(package => new PickerToggle(
                RichString.Rich(Theme.AccentMarkup(package.Label, package.Accent)),
                () => _packages.Contains(package.Key),
                selected => SetPackage(package.Key, selected))));

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

    private static Sprite? CategoryIcon(MapEntityCategory category)
        => GUIStyle.GetComponentStyle(new Identifier("CategoryButton." + category))
            is { } style && style.Sprites.TryGetValue(GUIComponent.ComponentState.None, out List<UISprite>? sprites)
            && sprites.Count > 0
                ? sprites[0].Sprite
                : null;

    private void SetPackage(string package, bool selected)
    {
        if (selected) { _packages.Add(package); }
        else { _packages.Remove(package); }

        _categories &= ItemPrefabCatalog.CategoryMaskIn(_packages);

        Refresh();
    }

    private void SetCategory(MapEntityCategory category, bool selected)
    {
        _categories = selected ? _categories | category : _categories & ~category;

        Refresh();
    }

    private void Refresh(bool keepScroll = false)
    {
        _keepScroll |= keepScroll;

        if (NotificationBatch.Defer(this, nameof(Refresh), RefreshRows)) { return; }

        RefreshRows();
    }

    private void RefreshRows()
    {
        bool keepScroll = _keepScroll;
        _keepScroll = false;

        ItemFilter filter = new(_packages, _categories);
        string query = _query.Trim();

        _visible = _catalog.Select(
            entry => Shows(entry, filter, query),
            entry => new ItemPickerRowViewModel(entry, Picked, UseOnSelf, _hintTarget));

        using (NotificationBatch.Begin())
        {
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
}
