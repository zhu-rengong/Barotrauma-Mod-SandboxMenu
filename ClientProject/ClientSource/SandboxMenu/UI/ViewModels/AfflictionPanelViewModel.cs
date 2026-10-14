using System.Runtime.CompilerServices;

namespace SandboxMenu.UI.ViewModels;

internal sealed class AfflictionPanelViewModel : Notifiable
{

    private const float VisibleStrength = 0.05f;

    private static readonly LimbType[] PreferredLimbs =
    [
        LimbType.Torso, LimbType.Head, LimbType.Waist,
        LimbType.LeftArm, LimbType.RightArm,
        LimbType.LeftLeg, LimbType.RightLeg,
        LimbType.LeftForearm, LimbType.RightForearm,
        LimbType.LeftThigh, LimbType.RightThigh,
        LimbType.LeftHand, LimbType.RightHand,
        LimbType.LeftFoot, LimbType.RightFoot,
        LimbType.Legs, LimbType.Tail, LimbType.Jaw, LimbType.None
    ];

    private readonly IDialogHost _host;
    private readonly HashSet<string> _packages = new(PackageLabels.KeyComparer);
    private readonly HashSet<Identifier> _types = [];
    private readonly HashSet<int> _targets = [];
    private readonly List<AfflictionTileViewModel> _tiles = [];
    private readonly List<AfflictionRowViewModel> _rows = [];

    private static readonly Dictionary<LimbType, int> LimbRanks = BuildRanks();

    private readonly Dictionary<int, Limb> _limbIndex = [];

    private IReadOnlyCollection<Affliction>? _afflictions;
    private CharacterHealth? _afflictionsFrom;

    private AfflictionPrefabEntry[] _entries = [];
    private AfflictionTileViewModel?[] _builtTiles = [];
    private int _tickParity;

    private IReadOnlyList<AfflictionTileViewModel> _visibleTiles = [];
    private IReadOnlyList<AfflictionRowViewModel> _visibleRows = [];
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
    private readonly Action<LocalizedString> _report;
    private string _query = string.Empty;
    private float _percent = -1f;
    private float _tileScroll;
    private float _rowScroll;
    private float _iconToolTipHealth = float.NaN;
    private int _shape;
    private bool _sortByName;
    private bool _hideEmptyLimbs = true;
    private bool _hideHidden = true;
    private readonly HashSet<AfflictionScope> _scopes = [];
    private readonly HashSet<AfflictionKind> _kinds = [];
    private bool _ready;

    internal AfflictionPanelViewModel(IDialogHost host, Action<LocalizedString> report)
    {
        _host = host;
        _report = report;

        OpenBrowserCommand = new RelayCommand(() => _host.ShowCharacterBrowser(Select));
        SelectControlledCommand = new RelayCommand(() => Select(Character.Controlled));
        PickTargetsCommand = new RelayCommand(PickTargets);
        DeletePickedCommand = new RelayCommand(() => Delete(_picked));
        PickPackagesCommand = new RelayCommand(PickPackages);
        PickTypesCommand = new RelayCommand(PickTypes);
        PickScopesCommand = new RelayCommand(PickScopes);
        PickKindsCommand = new RelayCommand(PickKinds);
    }

    public RelayCommand OpenBrowserCommand { get; }

    public RelayCommand SelectControlledCommand { get; }

    public RelayCommand PickTargetsCommand { get; }

    public RelayCommand DeletePickedCommand { get; }

    public RelayCommand PickPackagesCommand { get; }

    public RelayCommand PickTypesCommand { get; }

    public RelayCommand PickScopesCommand { get; }

    public RelayCommand PickKindsCommand { get; }

    public CharacterDisplay? Display => _display;

    public bool ShowIcon => _display is not null;

    public RichString IconToolTip => _display?.ToolTip ?? RichString.Rich(string.Empty);

    public RichString Title => _display is null
        ? RichString.Rich(TextManager.Get("sandboxmenu.afflictions.none"))
        : RichString.Rich(TooltipText.Bright(_display?.Name ?? string.Empty) + " " + TooltipText.Dim(_display?.Identifier ?? string.Empty));



    public RichString TargetLabel
        => RichString.Rich(_targets.Count == 0 ? TooltipText.Dim(TextManager.Get("sandboxmenu.afflictions.target.body")) : TargetName);

    public LocalizedString RowsEmptyHint => TextManager.Get("sandboxmenu.afflictions.rows.empty");

    public LocalizedString TilesEmptyHint => TextManager.Get("sandboxmenu.afflictions.tiles.empty");

    public bool RowsEmpty => _visibleRows.Count == 0;

    public bool TilesEmpty => _visibleTiles.Count == 0;

    public IReadOnlyList<AfflictionRowViewModel> Rows => _visibleRows;

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
            if (Set(ref _hideHidden, value)) { ApplyTileFilter(); }
        }
    }

    public string Query
    {
        get => _query;
        set
        {
            if (Set(ref _query, value ?? string.Empty)) { ApplyTileFilter(); }
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

    public LocalizedString PackageFilter => FilterLabel("sandboxmenu.filter.packages", _packages.Count);

    public LocalizedString TypeFilter => FilterLabel("sandboxmenu.afflictions.filter.types", _types.Count);

    public LocalizedString ScopeFilter => FilterLabel("sandboxmenu.afflictions.filter.scope", _scopes.Count);

    public LocalizedString KindFilter => FilterLabel("sandboxmenu.afflictions.filter.kind", _kinds.Count);

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
        _limbIndex.Clear();
        _picked = null;
        _iconToolTipHealth = float.NaN;
        _shape = 0;

        if (character is null)
        {
            _afflictions = null;
            _afflictionsFrom = null;
        }

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
            _visibleRows = [];

            using (NotificationBatch.Begin())
            {
                Raise(nameof(Rows));
                Raise(nameof(RowsEmpty));
            }

            return;
        }

        RebuildRows();
    }

    internal void Report(LocalizedString message) => _report(message);

    internal void ApplyResult(AfflictionStatus status)
        => Report(status switch
        {
            AfflictionStatus.Ok => TextManager.Get("sandboxmenu.afflictions.status.serverok"),
            AfflictionStatus.Denied => TextManager.Get("sandboxmenu.afflictions.status.serverdenied"),
            AfflictionStatus.UnknownAffliction => TextManager.Get("sandboxmenu.afflictions.status.serverunknown"),
            AfflictionStatus.NoCharacter => TextManager.Get("sandboxmenu.afflictions.status.servernocharacter"),
            AfflictionStatus.Throttled => TextManager.Get("sandboxmenu.afflictions.status.serverthrottled"),
            _ => TextManager.Get("sandboxmenu.afflictions.status.sendfailed")
        });

    internal void ReleaseContent()
    {
        _tiles.Clear();
        _entries = [];
        _builtTiles = [];
        _packages.Clear();
        _types.Clear();
        _visibleTiles = [];
        _ready = false;
        _percent = -1f;

        _display = null;
        _selectedRef = null;
        _targets.Clear();
        _limbIndex.Clear();
        _picked = null;
        _afflictions = null;
        _afflictionsFrom = null;
        _iconToolTipHealth = float.NaN;
        _shape = 0;
        _rows.Clear();
        _visibleRows = [];

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
            Raise(nameof(PackageFilter));
            Raise(nameof(TypeFilter));
        }
    }

    private LocalizedString TargetName
    {
        get
        {
            if (_targets.Count == 0) { return TextManager.Get("sandboxmenu.afflictions.target.body"); }

            LocalizedString joined = LocalizedString.EmptyString;
            string separator = TextManager.Get("sandboxmenu.list.separator").Value;

            foreach (Limb limb in Targets())
            {
                joined += joined.IsNullOrEmpty() ? LimbLabel(limb) : separator + LimbLabel(limb);
            }

            return joined;
        }
    }

    private IReadOnlyList<Limb> Targets()
    {
        List<Limb> limbs = [];

        foreach (Limb limb in GroupLimbs())
        {
            if (_targets.Contains(limb.HealthIndex)) { limbs.Add(limb); }
        }

        return limbs;
    }

    private void Prepare()
    {
        if (_ready) { return; }

        _ready = true;
        _percent = _percent < 0f ? 100f : _percent;

        _entries = [.. AfflictionPrefabCatalog.All()];
        _builtTiles = new AfflictionTileViewModel?[_entries.Length];
        _tiles.Clear();

        Raise(nameof(StrengthPercent));

        ApplyTileFilter();
    }

    private void Apply(AfflictionTileViewModel tile)
    {
        if (_selected is not { } character)
        {
            Report(TextManager.Get("sandboxmenu.status.nocharacter"));
            return;
        }

        AfflictionPrefab prefab = tile.Entry.Prefab;
        float max = prefab.MaxStrength > 0f ? prefab.MaxStrength : 1f;
        float strength = MathHelper.Clamp(max * Math.Clamp(_percent, 1f, 100f) / 100f, 1f, max);
        AfflictionTargets targets = new(_targets.Count == 0, Targets());

        AfflictionOutcome outcome = ClientAfflictionDispatcher.Apply(character, prefab, strength, targets);

        Report(outcome switch
        {
            AfflictionOutcome.Sent => TextManager.Get("sandboxmenu.afflictions.status.senttoserver"),
            AfflictionOutcome.Applied => TextManager.GetWithVariable("sandboxmenu.afflictions.status.applied", "[value]", tile.Display.Name),
            AfflictionOutcome.Blocked => TextManager.Get("sandboxmenu.afflictions.status.blocked"),
            _ => TextManager.Get("sandboxmenu.afflictions.status.sendfailed")
        });
    }

    private void PickTargets()
    {
        IReadOnlyList<Limb> limbs = GroupLimbs();

        if (limbs.Count == 0) { return; }

        _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.afflictions.targets"),
            limbs.Select(limb => new PickerToggle(
                LimbLabel(limb),
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

    private void OpenMenu(AfflictionItemRowViewModel row)
    {
        PickRow(row);

        _host.ShowContextMenu(
        [
            new MenuCommand("sandboxmenu.delete", () => Delete(row), TextManager.Get("sandboxmenu.shortcut.delete"))
        ]);
    }

    private void Delete(AfflictionItemRowViewModel? row)
    {
        if (row is null || _selected is not { } character)
        {
            Report(TextManager.Get("sandboxmenu.status.nocharacter"));
            return;
        }

        AfflictionPrefab prefab = row.Prefab;

        if (row.Selected) { _picked = null; }

        AfflictionOutcome outcome = ClientAfflictionDispatcher.Remove(character, prefab, row.HealthIndex);

        Report(outcome switch
        {
            AfflictionOutcome.Sent => TextManager.Get("sandboxmenu.afflictions.status.senttoserver"),
            AfflictionOutcome.Removed => TextManager.GetWithVariable("sandboxmenu.afflictions.status.removed", "[value]", row.Display.Name),
            _ => TextManager.Get("sandboxmenu.afflictions.status.sendfailed")
        });
    }

    private void RefreshRows()
    {
        if (_selected is null) { return; }

        CharacterHealth health = _selected.CharacterHealth;
        int shape = ShapeHash(health);

        if (shape != _shape)
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

        if (_selected?.CharacterHealth is not { } health)
        {
            _shape = 0;
            PublishRows();
            return;
        }

        _shape = ShapeHash(health);

        List<Affliction> general = [];
        Dictionary<int, List<Affliction>> byLimb = [];

        foreach (Affliction affliction in AfflictionsOf(health))
        {
            if (affliction.Strength < VisibleStrength) { continue; }

            if (health.GetAfflictionLimb(affliction) is not { } limb) { general.Add(affliction); continue; }

            if (!byLimb.TryGetValue(limb.HealthIndex, out List<Affliction>? found)) { byLimb[limb.HealthIndex] = found = []; }

            found.Add(affliction);
        }

        AddGroup(health, null, TextManager.Get("sandboxmenu.afflictions.group.body"), general);

        foreach (Limb limb in GroupLimbs())
        {
            byLimb.Remove(limb.HealthIndex, out List<Affliction>? found);
            AddGroup(health, limb, LimbLabel(limb), found);
        }

        foreach (KeyValuePair<int, List<Affliction>> pair in byLimb)
        {
            AddGroup(health, null, TextManager.Get("sandboxmenu.afflictions.group.other"), pair.Value);
        }

        PublishRows();

        if (picked is not null) { Pick(picked); }
    }

    private void Pick(Affliction affliction)
    {
        foreach (AfflictionRowViewModel row in _rows)
        {
            if (row is not AfflictionItemRowViewModel item || !ReferenceEquals(item.Affliction, affliction)) { continue; }

            _picked = item;
            item.Selected = true;

            return;
        }
    }

    private void AddGroup(CharacterHealth health, Limb? limb, LocalizedString label, List<Affliction>? found)
    {
        int count = found?.Count ?? 0;

        if (count == 0 && _hideEmptyLimbs && limb is not null) { return; }

        _rows.Add(new AfflictionGroupRowViewModel(label) { Count = count });

        if (found is null) { return; }

        found.Sort(Compare);

        foreach (Affliction affliction in found)
        {
            AfflictionItemRowViewModel row = new(affliction, health, limb, PickRow, OpenMenu);
            row.Refresh();
            _rows.Add(row);
        }
    }

    private void PublishRows()
    {
        _visibleRows = [.. _rows];

        Raise(nameof(Rows));
        Raise(nameof(RowsEmpty));
    }

    private int Compare(Affliction a, Affliction b)
        => _sortByName
            ? string.Compare(NameOf(a), NameOf(b), StringComparison.OrdinalIgnoreCase)
            : b.Strength.CompareTo(a.Strength);

    private static string NameOf(Affliction affliction) => AfflictionDisplay.For(affliction.Prefab).Name.Value;

    private IReadOnlyList<Limb> GroupLimbs()
    {
        if (_selected?.AnimController is not { } controller) { return []; }

        _limbIndex.Clear();

        foreach (Limb limb in controller.Limbs)
        {
            if (limb.HealthIndex < 0) { continue; }

            if (!_limbIndex.TryGetValue(limb.HealthIndex, out Limb? kept) || IsBetterName(limb, kept)) { _limbIndex[limb.HealthIndex] = limb; }
        }

        List<Limb> limbs = new(_limbIndex.Count);

        limbs.AddRange(_limbIndex.Values);
        limbs.Sort(static (left, right) => left.HealthIndex.CompareTo(right.HealthIndex));

        return limbs;
    }

    private static bool IsBetterName(Limb candidate, Limb current)
        => candidate.Hidden == current.Hidden ? Rank(candidate) < Rank(current) : !candidate.Hidden;

    private static int Rank(Limb limb) => LimbRanks.TryGetValue(limb.type, out int rank) ? rank : int.MaxValue;

    private static Dictionary<LimbType, int> BuildRanks()
    {
        Dictionary<LimbType, int> ranks = [];

        for (int i = 0; i < PreferredLimbs.Length; i++) { ranks.TryAdd(PreferredLimbs[i], i); }

        return ranks;
    }

    private static LocalizedString LimbLabel(Limb limb)
    {
        LocalizedString label = TextManager.Get("sandboxmenu.limb." + limb.type.ToString().ToLowerInvariant());

        return label.IsNullOrWhiteSpace() ? limb.Name : label;
    }

    private int ShapeHash(CharacterHealth health)
    {
        int hash = 17;

        foreach (Affliction affliction in AfflictionsOf(health))
        {
            if (affliction.Strength < VisibleStrength) { continue; }

            hash = hash * 31 + RuntimeHelpers.GetHashCode(affliction);
            hash = hash * 31 + (health.GetAfflictionLimb(affliction)?.HealthIndex ?? -1);
        }

        return hash;
    }

    private IReadOnlyCollection<Affliction> AfflictionsOf(CharacterHealth health)
    {
        if (!ReferenceEquals(_afflictionsFrom, health) || _afflictions is null)
        {
            _afflictionsFrom = health;
            _afflictions = health.GetAllAfflictions();
        }

        return _afflictions;
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

        ApplyTileFilter();
    }

    private void SetType(Identifier type, bool selected)
    {
        if (selected) { _types.Add(type); }
        else { _types.Remove(type); }

        ApplyTileFilter();
    }

    private void SetScope(AfflictionScope scope, bool selected)
    {
        if (selected) { _scopes.Add(scope); }
        else { _scopes.Remove(scope); }

        ApplyTileFilter();
    }

    private void SetKind(AfflictionKind kind, bool selected)
    {
        if (selected) { _kinds.Add(kind); }
        else { _kinds.Remove(kind); }

        ApplyTileFilter();
    }

    private void ApplyTileFilter()
    {
        if (NotificationBatch.Defer(this, nameof(ApplyTileFilter), RenewTileFilter)) { return; }

        RenewTileFilter();
    }

    private void RenewTileFilter()
    {
        string query = _query.Trim();
        AfflictionFilter filter = new(_packages, _types, _scopes, _kinds);
        List<AfflictionTileViewModel> visible = new(_entries.Length);

        for (int i = 0; i < _entries.Length; i++)
        {
            AfflictionPrefabEntry entry = _entries[i];

            if (_hideHidden && entry.Hidden) { continue; }
            if (!filter.Allows(entry)) { continue; }
            if (query.Length != 0 && !entry.Matches(query)) { continue; }

            AfflictionTileViewModel? tile = _builtTiles[i];

            if (tile is null)
            {
                _builtTiles[i] = tile = new AfflictionTileViewModel(entry, _percent, Apply);
                _tiles.Add(tile);
            }

            visible.Add(tile);
        }

        _visibleTiles = visible;

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

    private static LocalizedString FilterLabel(string nameKey, int chosen)
    {
        LocalizedString count = chosen.ToString();

        return TextManager.GetWithVariables(
            chosen == 0 ? "sandboxmenu.filter.summary.all" : "sandboxmenu.filter.summary.some",
            ("[name]", TextManager.Get(nameKey)),
            ("[count]", count));
    }
}
