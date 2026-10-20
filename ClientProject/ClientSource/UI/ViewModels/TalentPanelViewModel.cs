namespace SandboxMenu.UI.ViewModels;

internal sealed class TalentPanelViewModel : Notifiable
{
    private readonly IDialogHost _host;
    private readonly HashSet<string> _packages = new(PackageLabels.KeyComparer);
    private readonly HashSet<Identifier> _jobs = [];
    private readonly HashSet<Identifier> _subTrees = [];
    private readonly List<TalentRowViewModel> _rows = [];
    private readonly List<TalentTileViewModel> _tiles = [];

    private FilteredCatalog<TalentPrefabEntry, TalentTileViewModel>? _catalog;
    private int _tickParity;

    private IReadOnlyList<TalentTileViewModel> _visibleTiles = [];
    private WeakReference<Character>? _selectedRef;

    private Character? _selected
    {
        get
        {
            if (_selectedRef is not { } reference) { return null; }
            if (!reference.TryGetTarget(out Character? character) || character is null) { return null; }

            return character.Removed || !Character.CharacterList.Contains(character) || !ClientRequestPolicy.CanTarget(character)
                ? null
                : character;
        }
    }
    private CharacterDisplay? _display;
    private readonly Action<StatusKind, LocalizedString> _report;
    private string _query = string.Empty;
    private float _tileScroll;
    private float _rowScroll;
    private bool _hideHidden = true;
    private bool _ready;
    private int _shape;

    internal TalentPanelViewModel(IDialogHost host, Action<StatusKind, LocalizedString> report)
    {
        _host = host;
        _report = report;

        OpenBrowserCommand = new RelayCommand(() => _host.ShowCharacterBrowser(Select, static character => character.Info is not null));
        SelectControlledCommand = new RelayCommand(() => Select(Character.Controlled));
        PickPackagesCommand = new RelayCommand(PickPackages);
        PickJobsCommand = new RelayCommand(PickJobs);
        PickSubTreesCommand = new RelayCommand(PickSubTrees);
    }

    public RelayCommand OpenBrowserCommand { get; }

    public RelayCommand SelectControlledCommand { get; }

    public RelayCommand PickPackagesCommand { get; }

    public RelayCommand PickJobsCommand { get; }

    public RelayCommand PickSubTreesCommand { get; }

    public CharacterDisplay? Display => _display;

    public bool ShowIcon => _display is not null;

    public RichString IconToolTip => _display?.ToolTip ?? RichString.Rich(string.Empty);

    public RichString Title => _display is null
        ? RichString.Rich(TextManager.Get("sandboxmenu.talents.none"))
        : RichString.Rich(TooltipText.Bright(_display.Name));

    public LocalizedString RowsEmptyHint
        => TextManager.Get(_display is null ? "sandboxmenu.talents.rows.empty" : "sandboxmenu.talents.rows.none");

    public LocalizedString TilesEmptyHint => TextManager.Get("sandboxmenu.talents.tiles.empty");

    public bool RowsEmpty => _rows.Count == 0;

    public bool TilesEmpty => _visibleTiles.Count == 0;

    public IReadOnlyList<TalentRowViewModel> Rows => _rows;

    public IReadOnlyList<TalentTileViewModel> Tiles => _visibleTiles;

    public float RowScroll
    {
        get => _rowScroll;
        set => Set(ref _rowScroll, value);
    }

    public float TileScroll
    {
        get => _tileScroll;
        set => Set(ref _tileScroll, value);
    }

    public LocalizedString HiddenFilterLabel => TextManager.Get("sandboxmenu.filter.hide.hidden");

    public bool HideHidden
    {
        get => _hideHidden;
        set
        {
            if (Set(ref _hideHidden, value)) { RebuildTiles(); }
        }
    }

    public string Query
    {
        get => _query;
        set
        {
            if (Set(ref _query, value ?? string.Empty)) { RebuildTiles(); }
        }
    }

    public LocalizedString PackageFilter => FilterSummary.Label("sandboxmenu.filter.packages", _packages.Count);

    public LocalizedString JobFilter => FilterSummary.Label("sandboxmenu.talents.filter.jobs", _jobs.Count);

    public LocalizedString SubTreeFilter => FilterSummary.Label("sandboxmenu.talents.filter.subtrees", _subTrees.Count);

    internal void Tick()
    {
        if (_selected is not { } current) { SelectCore(Character.Controlled, force: _selectedRef is not null); }
        else if (_display is null) { SelectCore(current); }

        Prepare();

        _tickParity ^= 1;

        if (_tickParity == 0) { RefreshRows(); }
    }

    internal void Select(Character? character)
        => SelectCore(character, force: _selectedRef is not null && _selected is null);

    internal void ReleaseContent()
    {
        _catalog = null;
        _packages.Clear();
        _jobs.Clear();
        _subTrees.Clear();
        _visibleTiles = [];
        _tiles.Clear();
        _ready = false;
        _display = null;
        _selectedRef = null;
        _shape = 0;
        _rows.Clear();
        _tileScroll = 0f;
        _rowScroll = 0f;

        using (NotificationBatch.Begin())
        {
            Raise(nameof(Display));
            Raise(nameof(ShowIcon));
            Raise(nameof(IconToolTip));
            Raise(nameof(Title));
            Raise(nameof(Rows));
            Raise(nameof(RowsEmpty));
            Raise(nameof(RowsEmptyHint));
        }

        using (NotificationBatch.Begin())
        {
            Raise(nameof(Tiles));
            Raise(nameof(TilesEmpty));
            Raise(nameof(TileScroll));
            Raise(nameof(RowScroll));
            Raise(nameof(PackageFilter));
            Raise(nameof(JobFilter));
            Raise(nameof(SubTreeFilter));
        }
    }

    private void SelectCore(Character? character, bool force = false)
    {
        if (!ClientRequestPolicy.CanTarget(character)) { return; }
        if (!force && ReferenceEquals(_selected, character) && (character is null || _display is not null)) { return; }

        _selectedRef = character is null ? null : new WeakReference<Character>(character);
        _display = character is null ? null : new CharacterDisplay(character);
        _catalog = null;
        _visibleTiles = [];
        _shape = 0;

        using (NotificationBatch.Begin())
        {
            Raise(nameof(Display));
            Raise(nameof(ShowIcon));
            Raise(nameof(IconToolTip));
            Raise(nameof(Title));
        }

        Prepare();

        if (character is null)
        {
            _rows.Clear();
            PublishRows();

            return;
        }

        RebuildRows();
    }

    private void Prepare()
    {
        if (!_ready) { _ready = true; }
        if (_catalog is not null) { return; }

        _tiles.Clear();
        _catalog = new FilteredCatalog<TalentPrefabEntry, TalentTileViewModel>([.. TalentPrefabCatalog.All()]);

        RebuildTilesNow();
    }

    private void Apply(TalentTileViewModel tile)
    {
        if (_selected is not { } character)
        {
            Report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.nocharacter"));
            return;
        }

        if (character.Info is null)
        {
            Report(StatusKind.Warning, TextManager.Get("sandboxmenu.talents.status.noinfo"));
            return;
        }

        TalentPrefab prefab = tile.Prefab;

        if (character.HasTalent(prefab.Identifier))
        {
            Report(StatusKind.Warning, TextManager.GetWithVariable("sandboxmenu.talents.status.already", TooltipText.ValuePlaceholder, tile.Display.Name));
            return;
        }

        TalentOutcome outcome = ClientTalentDispatcher.Grant(character, prefab);

        (StatusKind kind, LocalizedString message) = outcome switch
        {
            TalentOutcome.Sent => (StatusKind.Success, TextManager.Get("sandboxmenu.talents.status.senttoserver")),
            TalentOutcome.Applied => (StatusKind.Success, TextManager.GetWithVariable("sandboxmenu.talents.status.granted", TooltipText.ValuePlaceholder, tile.Display.Name)),
            TalentOutcome.AlreadyKnown => (StatusKind.Warning, TextManager.GetWithVariable("sandboxmenu.talents.status.already", TooltipText.ValuePlaceholder, tile.Display.Name)),
            TalentOutcome.NoCharacter => (StatusKind.Warning, TextManager.Get("sandboxmenu.talents.status.noinfo")),
            TalentOutcome.Failed => (StatusKind.Failure, TextManager.Get("sandboxmenu.talents.status.failed")),
            TalentOutcome.Denied => (StatusKind.Failure, TextManager.Get("sandboxmenu.status.nopermission")),
            _ => (StatusKind.Failure, TextManager.Get("sandboxmenu.talents.status.sendfailed"))
        };

        Report(kind, message);

        if (outcome == TalentOutcome.Applied) { RebuildRows(); }
    }

    internal void ApplyResult(TalentStatus status)
    {
        (StatusKind kind, LocalizedString message) = status switch
        {
            TalentStatus.Ok => (StatusKind.Success, TextManager.Get("sandboxmenu.talents.status.serverok")),
            TalentStatus.Denied => (StatusKind.Failure, TextManager.Get("sandboxmenu.talents.status.serverdenied")),
            TalentStatus.UnknownTalent => (StatusKind.Failure, TextManager.Get("sandboxmenu.talents.status.serverunknown")),
            TalentStatus.NoCharacter => (StatusKind.Failure, TextManager.Get("sandboxmenu.talents.status.servernocharacter")),
            TalentStatus.Incompatible => (StatusKind.Failure, TextManager.Get("sandboxmenu.status.incompatible")),
            TalentStatus.NoResponse => (StatusKind.Warning, TextManager.Get("sandboxmenu.status.noresponse")),
            _ => (StatusKind.Failure, TextManager.Get("sandboxmenu.talents.status.sendfailed"))
        };

        Report(kind, message);

        if (status == TalentStatus.Ok)
        {
            _shape = 0;
            RefreshRows();
        }
    }

    private void RefreshRows()
    {
        if (_display?.Info is not { } info) { return; }
        if (Shape(info) == _shape) { return; }

        RebuildRows();
    }

    private void RebuildRows()
    {
        _rows.Clear();
        RefreshTileOwnership();

        if (_display?.Info is not { } info)
        {
            _shape = 0;
            PublishRows();

            return;
        }

        _shape = Shape(info);

        List<TalentPrefabEntry> owned = [];

        foreach (Identifier identifier in info.UnlockedTalents)
        {
            if (TalentPrefabCatalog.ByIdentifier(identifier) is { } entry) { owned.Add(entry); }
        }

        owned.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        foreach (TalentPrefabEntry entry in owned) { _rows.Add(new TalentRowViewModel(entry, info)); }

        PublishRows();
    }

    private void PublishRows()
    {
        Raise(nameof(Rows));
        Raise(nameof(RowsEmpty));
        Raise(nameof(RowsEmptyHint));
    }

    private void RefreshTileOwnership()
    {
        CharacterInfo? info = _display?.Info;

        for (int i = 0; i < _tiles.Count; i++)
        {
            _tiles[i].SetOwned(info is not null && info.UnlockedTalents.Contains(_tiles[i].Prefab.Identifier));
        }
    }

    private static int Shape(CharacterInfo info)
    {
        int hash = 17;

        foreach (Identifier identifier in info.UnlockedTalents) { hash = hash * 31 + identifier.GetHashCode(); }

        return hash;
    }

    private void PickPackages()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.filter.packages"),
            TalentPrefabCatalog.Packages().Select(option => new PickerToggle(
                RichString.Rich(Theme.AccentMarkup(option.Label, option.Accent)),
                () => _packages.Contains(option.Key),
                selected => SetPackage(option.Key, selected))));

    private void PickJobs()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.talents.filter.jobs"),
            TalentPrefabCatalog.Jobs().Select(job => new PickerToggle(
                RichString.Rich(Theme.AccentMarkup(job.Label, job.Accent)),
                () => _jobs.Contains(job.Identifier),
                selected => SetJob(job.Identifier, selected))));

    private void PickSubTrees()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.talents.filter.subtrees"),
            TalentPrefabCatalog.SubTreesIn(_jobs).Select(subTree => new PickerToggle(
                subTree.Label,
                () => _subTrees.Contains(subTree.Identifier),
                selected => SetSubTree(subTree.Identifier, selected))));

    private void SetPackage(string package, bool selected)
    {
        if (selected) { _packages.Add(package); }
        else { _packages.Remove(package); }

        RebuildTiles();
    }

    private void SetJob(Identifier job, bool selected)
    {
        if (selected) { _jobs.Add(job); }
        else { _jobs.Remove(job); }

        HashSet<Identifier> available = [.. TalentPrefabCatalog.SubTreesIn(_jobs).Select(static subTree => subTree.Identifier)];

        _subTrees.RemoveWhere(subTree => !available.Contains(subTree));

        RebuildTiles();
    }

    private void SetSubTree(Identifier subTree, bool selected)
    {
        if (selected) { _subTrees.Add(subTree); }
        else { _subTrees.Remove(subTree); }

        RebuildTiles();
    }

    private void RebuildTiles()
    {
        if (NotificationBatch.Defer(this, nameof(RebuildTiles), RebuildTilesNow)) { return; }

        RebuildTilesNow();
    }

    private void RebuildTilesNow()
    {
        string query = _query.Trim();
        TalentFilter filter = new(_packages, _jobs, _subTrees);
        CharacterInfo? info = _display?.Info;

        _visibleTiles = _catalog is null
            ? []
            : _catalog.Select(
                entry => !(_hideHidden && entry.Hidden)
                    && filter.Allows(entry)
                    && (query.Length == 0 || entry.Matches(query)),
                entry => BuildTile(entry, info));

        using (NotificationBatch.Begin())
        {
            _tileScroll = 0f;

            Raise(nameof(TileScroll));
            Raise(nameof(Tiles));
            Raise(nameof(TilesEmpty));
            Raise(nameof(PackageFilter));
            Raise(nameof(JobFilter));
            Raise(nameof(SubTreeFilter));
        }
    }

    private TalentTileViewModel BuildTile(TalentPrefabEntry entry, CharacterInfo? info)
    {
        TalentTileViewModel tile = new(
            entry,
            info,
            info is not null && info.UnlockedTalents.Contains(entry.Prefab.Identifier),
            Apply);

        _tiles.Add(tile);

        return tile;
    }

    private void Report(StatusKind kind, LocalizedString message) => _report(kind, message);
}
