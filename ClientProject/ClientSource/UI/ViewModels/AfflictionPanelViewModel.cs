namespace SandboxMenu.UI.ViewModels;

internal sealed class AfflictionPanelViewModel : Notifiable
{
    private readonly IDialogHost _host;
    private readonly HashSet<string> _packages = new(PackageLabels.KeyComparer);
    private readonly HashSet<Identifier> _types = [];
    private readonly HashSet<int> _targets = [];
    private readonly List<AfflictionTileViewModel> _tiles = [];
    private readonly List<Notifiable> _rows = [];

    private FilteredCatalog<AfflictionPrefabEntry, AfflictionTileViewModel>? _catalog;
    private int _tickParity;

    private IReadOnlyList<AfflictionTileViewModel> _visibleTiles = [];
    private WeakReference<Character>? _selectedRef;

    private Character? _selected
    {
        get
        {
            if (_selectedRef is not { } reference) { return null; }
            if (!reference.TryGetTarget(out Character? character) || character is null) { return null; }

            return character.Removed || !Character.CharacterList.Contains(character) ? null : character;
        }
    }
    private CharacterDisplay? _display;
    private AfflictionItemRowViewModel? _picked;
    private readonly Action<StatusKind, LocalizedString> _report;
    private string _query = string.Empty;
    private float _percent = -1f;
    private float _tileScroll;
    private float _rowScroll;
    private float _iconToolTipHealth = float.NaN;
    private int _shape;
    private bool _sortByName;
    private bool _hideEmptyLimbs = true;
    private bool _hideHidden = true;
    private bool _multiplyByMaxVitality;
    private readonly HashSet<AfflictionScope> _scopes = [];
    private readonly HashSet<AfflictionKind> _kinds = [];
    private bool _ready;

    internal AfflictionPanelViewModel(IDialogHost host, Action<StatusKind, LocalizedString> report)
    {
        _host = host;
        _report = report;

        OpenBrowserCommand = new RelayCommand(() => _host.ShowCharacterBrowser(Select));
        SelectControlledCommand = new RelayCommand(() => Select(Character.Controlled));
        PickTargetsCommand = new RelayCommand(PickTargets);
        RemovePickedCommand = new RelayCommand(() => Remove(_picked));
        PickPackagesCommand = new RelayCommand(PickPackages);
        PickTypesCommand = new RelayCommand(PickTypes);
        PickScopesCommand = new RelayCommand(PickScopes);
        PickKindsCommand = new RelayCommand(PickKinds);
    }

    public RelayCommand OpenBrowserCommand { get; }

    public RelayCommand SelectControlledCommand { get; }

    public RelayCommand PickTargetsCommand { get; }

    public RelayCommand RemovePickedCommand { get; }

    public RelayCommand PickPackagesCommand { get; }

    public RelayCommand PickTypesCommand { get; }

    public RelayCommand PickScopesCommand { get; }

    public RelayCommand PickKindsCommand { get; }

    public CharacterDisplay? Display => _display;

    public bool ShowIcon => _display is not null;

    public RichString IconToolTip => _display?.ToolTip ?? RichString.Rich(string.Empty);

    public RichString Title => _display is null
        ? RichString.Rich(TextManager.Get("sandboxmenu.afflictions.none"))
        : RichString.Rich(TooltipText.Bright(_display?.Name ?? string.Empty));

    public RichString TargetLabel
        => RichString.Rich(_targets.Count == 0 ? TooltipText.Dim(TextManager.Get("sandboxmenu.afflictions.target.body")) : TargetName);

    public LocalizedString RowsEmptyHint => TextManager.Get("sandboxmenu.afflictions.rows.empty");

    public LocalizedString TilesEmptyHint => TextManager.Get("sandboxmenu.afflictions.tiles.empty");

    public bool RowsEmpty => _rows.Count == 0;

    public bool TilesEmpty => _visibleTiles.Count == 0;

    public IReadOnlyList<Notifiable> Rows => _rows;

    public IReadOnlyList<AfflictionTileViewModel> Tiles => _visibleTiles;

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

    public bool SortByName
    {
        get => _sortByName;
        set
        {
            if (Set(ref _sortByName, value)) { RebuildRows(); }
        }
    }

    public bool HideEmptyLimbs
    {
        get => _hideEmptyLimbs;
        set
        {
            if (Set(ref _hideEmptyLimbs, value)) { RebuildRows(); }
        }
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

    public float StrengthPercent
    {
        get => _percent;
        set
        {
            float clamped = Math.Clamp(value, 0f, 100f);
            if (!Set(ref _percent, clamped)) { return; }

            for (int i = 0; i < _tiles.Count; i++) { _tiles[i].SetStrength(clamped); }
        }
    }

    public bool MultiplyByMaxVitality
    {
        get => _multiplyByMaxVitality;
        set => Set(ref _multiplyByMaxVitality, value);
    }

    public LocalizedString PackageFilter => FilterSummary.Label("sandboxmenu.filter.packages", _packages.Count);

    public LocalizedString TypeFilter => FilterSummary.Label("sandboxmenu.afflictions.filter.types", _types.Count);

    public LocalizedString ScopeFilter => FilterSummary.Label("sandboxmenu.afflictions.filter.scope", _scopes.Count);

    public LocalizedString KindFilter => FilterSummary.Label("sandboxmenu.afflictions.filter.kind", _kinds.Count);

    internal void Tick()
    {
        if (_selected is not { } current) { SelectCore(Character.Controlled, force: _selectedRef is not null); }
        else if (_display is null) { SelectCore(current); }

        Prepare();

        _tickParity ^= 1;

        if (_tickParity == 0) { RefreshRows(); }
    }

    internal void RefreshIconToolTip()
    {
        if (_selected is not { } selected) { return; }

        float health = selected.Vitality;

        if (health == _iconToolTipHealth) { return; }

        _iconToolTipHealth = health;

        Raise(nameof(IconToolTip));
    }

    internal void Select(Character? character)
        => SelectCore(character, force: _selectedRef is not null && _selected is null);

    private void SelectCore(Character? character, bool force = false)
    {
        if (!force && ReferenceEquals(_selected, character) && (character is null || _display is not null)) { return; }

        _selectedRef = character is null ? null : new WeakReference<Character>(character);
        _display = character is null ? null : new CharacterDisplay(character);
        _targets.Clear();
        _picked = null;
        _iconToolTipHealth = float.NaN;
        _shape = 0;

        using (NotificationBatch.Begin())
        {
            Raise(nameof(Display));
            Raise(nameof(ShowIcon));
            Raise(nameof(IconToolTip));
            Raise(nameof(Title));
            Raise(nameof(TargetLabel));
        }

        if (character is null)
        {
            _rows.Clear();

            using (NotificationBatch.Begin())
            {
                Raise(nameof(Rows));
                Raise(nameof(RowsEmpty));
            }

            return;
        }

        RebuildRows();
    }

    internal void Report(StatusKind kind, LocalizedString message) => _report(kind, message);

    internal void ApplyResult(AfflictionStatus status)
    {
        (StatusKind kind, LocalizedString message) = status switch
        {
            AfflictionStatus.Ok => (StatusKind.Success, TextManager.Get("sandboxmenu.afflictions.status.serverok")),
            AfflictionStatus.Denied => (StatusKind.Failure, TextManager.Get("sandboxmenu.afflictions.status.serverdenied")),
            AfflictionStatus.UnknownAffliction => (StatusKind.Failure, TextManager.Get("sandboxmenu.afflictions.status.serverunknown")),
            AfflictionStatus.NoCharacter => (StatusKind.Failure, TextManager.Get("sandboxmenu.afflictions.status.servernocharacter")),
            AfflictionStatus.Throttled => (StatusKind.Warning, TextManager.Get("sandboxmenu.afflictions.status.serverthrottled")),
            AfflictionStatus.Blocked => (StatusKind.Failure, TextManager.Get("sandboxmenu.afflictions.status.blocked")),
            _ => (StatusKind.Failure, TextManager.Get("sandboxmenu.afflictions.status.sendfailed"))
        };

        Report(kind, message);
    }

    internal void ReleaseContent()
    {
        _tiles.Clear();
        _catalog = null;
        _packages.Clear();
        _types.Clear();
        _visibleTiles = [];
        _ready = false;
        _percent = -1f;
        _multiplyByMaxVitality = false;

        _display = null;
        _selectedRef = null;
        _targets.Clear();
        _picked = null;
        _iconToolTipHealth = float.NaN;
        _shape = 0;
        _rows.Clear();

        using (NotificationBatch.Begin())
        {
            Raise(nameof(Display));
            Raise(nameof(ShowIcon));
            Raise(nameof(IconToolTip));
            Raise(nameof(Title));
            Raise(nameof(TargetLabel));
            Raise(nameof(Rows));
            Raise(nameof(RowsEmpty));
        }

        using (NotificationBatch.Begin())
        {
            Raise(nameof(Tiles));
            Raise(nameof(TilesEmpty));
            Raise(nameof(StrengthPercent));
            Raise(nameof(MultiplyByMaxVitality));
            Raise(nameof(PackageFilter));
            Raise(nameof(TypeFilter));
        }
    }

    private LocalizedString TargetName
    {
        get
        {
            if (_targets.Count == 0) { return TextManager.Get("sandboxmenu.afflictions.target.body"); }

            return _display is null ? LocalizedString.EmptyString : _display.LimbNames(_display.LimbsIn(_targets));
        }
    }

    private IReadOnlyList<Limb> Targets() => _display is null ? [] : _display.LimbsIn(_targets);

    private void Prepare()
    {
        if (_ready) { return; }

        _ready = true;
        _percent = _percent < 0f ? 100f : _percent;

        _catalog = new FilteredCatalog<AfflictionPrefabEntry, AfflictionTileViewModel>([.. AfflictionPrefabCatalog.All()]);
        _tiles.Clear();

        Raise(nameof(StrengthPercent));

        RebuildTiles();
    }

    private void Apply(AfflictionTileViewModel tile)
    {
        if (_selected is not { } character)
        {
            Report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.nocharacter"));
            return;
        }

        AfflictionPrefab prefab = tile.Entry.Prefab;
        float max = prefab.MaxStrength > 0f ? prefab.MaxStrength : 1f;
        float strength = MathHelper.Clamp(max * Math.Clamp(_percent, 1f, 100f) / 100f, 1f, max);
        AfflictionTargets targets = new(_targets.Count == 0, Targets());

        AfflictionOutcome outcome = ClientAfflictionDispatcher.Apply(character, prefab, strength, targets, _multiplyByMaxVitality);

        (StatusKind kind, LocalizedString message) = outcome switch
        {
            AfflictionOutcome.Sent => (StatusKind.Success, TextManager.Get("sandboxmenu.afflictions.status.senttoserver")),
            AfflictionOutcome.Applied => (StatusKind.Success, TextManager.GetWithVariable("sandboxmenu.afflictions.status.applied", "[value]", tile.Display.Name)),
            AfflictionOutcome.Blocked => (StatusKind.Failure, TextManager.Get("sandboxmenu.afflictions.status.blocked")),
            _ => (StatusKind.Failure, TextManager.Get("sandboxmenu.afflictions.status.sendfailed"))
        };

        Report(kind, message);
    }

    private void PickTargets()
    {
        IReadOnlyList<Limb> limbs = _display is null ? [] : _display.Limbs();

        if (limbs.Count == 0) { return; }

        _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.afflictions.targets"),
            limbs.Select(limb => new PickerToggle(
                CharacterDisplay.LimbLabel(limb),
                () => _targets.Contains(limb.HealthIndex),
                selected => SetTarget(limb.HealthIndex, selected))));
    }

    private void SetTarget(int healthIndex, bool selected)
    {
        if (selected) { _targets.Add(healthIndex); }
        else { _targets.Remove(healthIndex); }

        Raise(nameof(TargetLabel));
    }

    private void PickRow(AfflictionItemRowViewModel row)
    {
        if (!ReferenceEquals(_picked, row) && _picked is { } previous) { previous.Selected = false; }

        _picked = row;
        row.Selected = true;
    }

    private void ShowContextMenu(AfflictionItemRowViewModel row)
    {
        PickRow(row);

        _host.ShowContextMenu(
        [
            new MenuCommand("sandboxmenu.afflictions.remove", () => Remove(row), TextManager.Get("sandboxmenu.shortcut.delete"))
        ]);
    }

    private void Remove(AfflictionItemRowViewModel? row)
    {
        if (row is null || _selected is not { } character)
        {
            Report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.nocharacter"));
            return;
        }

        AfflictionPrefab prefab = row.Prefab;

        if (row.Selected) { _picked = null; }

        AfflictionOutcome outcome = ClientAfflictionDispatcher.Remove(character, prefab, row.LimbHealthIndex);

        (StatusKind kind, LocalizedString message) = outcome switch
        {
            AfflictionOutcome.Sent => (StatusKind.Success, TextManager.Get("sandboxmenu.afflictions.status.senttoserver")),
            AfflictionOutcome.Removed => (StatusKind.Success, TextManager.GetWithVariable("sandboxmenu.afflictions.status.removed", "[value]", row.Display.Name)),
            _ => (StatusKind.Failure, TextManager.Get("sandboxmenu.afflictions.status.sendfailed"))
        };

        Report(kind, message);
    }

    private void RefreshRows()
    {
        if (_display is not { } display || display.CharacterHealth is null) { return; }

        if (display.Shape() != _shape)
        {
            RebuildRows();
            return;
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] is AfflictionItemRowViewModel item) { item.Refresh(); }
        }
    }

    private void RebuildRows()
    {
        Affliction? picked = _picked?.Affliction;

        _picked = null;
        _rows.Clear();

        if (_display is not { } display || display.CharacterHealth is not { } health)
        {
            _shape = 0;
            PublishRows();
            return;
        }

        _shape = display.Shape();

        foreach (CharacterDisplay.AfflictionGroup group in display.Groups(_hideEmptyLimbs, _sortByName))
        {
            _rows.Add(new AfflictionGroupRowViewModel(group.Label) { Count = group.Items.Count });

            foreach (Affliction affliction in group.Items)
            {
                AfflictionItemRowViewModel row = new(affliction, health, group.Limb, PickRow, ShowContextMenu);
                row.Refresh();
                _rows.Add(row);
            }
        }

        PublishRows();

        if (picked is not null) { Pick(picked); }
    }

    private void Pick(Affliction affliction)
    {
        foreach (Notifiable row in _rows)
        {
            if (row is not AfflictionItemRowViewModel item || !ReferenceEquals(item.Affliction, affliction)) { continue; }

            _picked = item;
            item.Selected = true;

            return;
        }
    }

    private void PublishRows()
    {
        Raise(nameof(Rows));
        Raise(nameof(RowsEmpty));
    }

    private void PickPackages()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.filter.packages"),
            AfflictionPrefabCatalog.Packages().Select(option => new PickerToggle(
                RichString.Rich(Theme.AccentMarkup(option.Label, option.Accent)),
                () => _packages.Contains(option.Key),
                selected => SetPackage(option.Key, selected))));

    private void PickTypes()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.afflictions.filter.types"),
            AfflictionPrefabCatalog.TypesIn(_packages).Select(type => new PickerToggle(
                type.Value,
                () => _types.Contains(type),
                selected => SetType(type, selected))));

    private void PickScopes()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.afflictions.filter.scope"),
            [
                new PickerToggle(
                    RichString.Rich(TextManager.Get("sandboxmenu.afflictions.option.limb")),
                    () => _scopes.Contains(AfflictionScope.Limb),
                    selected => SetScope(AfflictionScope.Limb, selected)),
                new PickerToggle(
                    RichString.Rich(TextManager.Get("sandboxmenu.afflictions.option.body")),
                    () => _scopes.Contains(AfflictionScope.Body),
                    selected => SetScope(AfflictionScope.Body, selected))
            ]);

    private void PickKinds()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.afflictions.filter.kind"),
            [
                new PickerToggle(
                    RichString.Rich(TextManager.Get("sandboxmenu.afflictions.option.buff")),
                    () => _kinds.Contains(AfflictionKind.Buff),
                    selected => SetKind(AfflictionKind.Buff, selected)),
                new PickerToggle(
                    RichString.Rich(TextManager.Get("sandboxmenu.afflictions.option.debuff")),
                    () => _kinds.Contains(AfflictionKind.Debuff),
                    selected => SetKind(AfflictionKind.Debuff, selected))
            ]);

    private void SetPackage(string package, bool selected)
    {
        if (selected) { _packages.Add(package); }
        else { _packages.Remove(package); }

        HashSet<Identifier> available = [.. AfflictionPrefabCatalog.TypesIn(_packages)];
        _types.RemoveWhere(type => !available.Contains(type));

        RebuildTiles();
    }

    private void SetType(Identifier type, bool selected)
    {
        if (selected) { _types.Add(type); }
        else { _types.Remove(type); }

        RebuildTiles();
    }

    private void SetScope(AfflictionScope scope, bool selected)
    {
        if (selected) { _scopes.Add(scope); }
        else { _scopes.Remove(scope); }

        RebuildTiles();
    }

    private void SetKind(AfflictionKind kind, bool selected)
    {
        if (selected) { _kinds.Add(kind); }
        else { _kinds.Remove(kind); }

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
        AfflictionFilter filter = new(_packages, _types, _scopes, _kinds);

        _visibleTiles = _catalog is null
            ? []
            : _catalog.Select(
                entry => !(_hideHidden && entry.Hidden)
                    && filter.Allows(entry)
                    && (query.Length == 0 || entry.Matches(query)),
                BuildTile);

        using (NotificationBatch.Begin())
        {
            _tileScroll = 0f;

            Raise(nameof(TileScroll));
            Raise(nameof(Tiles));
            Raise(nameof(TilesEmpty));
            Raise(nameof(PackageFilter));
            Raise(nameof(TypeFilter));
            Raise(nameof(ScopeFilter));
            Raise(nameof(KindFilter));
        }
    }

    private AfflictionTileViewModel BuildTile(AfflictionPrefabEntry entry)
    {
        AfflictionTileViewModel tile = new(entry, _percent, Apply);

        _tiles.Add(tile);

        return tile;
    }
}
