namespace SandboxMenu.UI.ViewModels;

internal sealed record SpeciesOption(Identifier Species, LocalizedString Label);

internal sealed record TeamOption(CharacterTeamType Team, LocalizedString Label);

internal sealed class CharacterTileViewModel(CharacterDisplay display, Action<Character> onPicked) : Notifiable
{
    private float _toolTipHealth = float.NaN;
    private bool _disabled = display.Removed;

    public CharacterDisplay Display { get; } = display;

    public RelayCommand PickCommand { get; } = new(() =>
    {
        if (display.Removed) { return; }

        onPicked(display.Character);
    });

    public bool Disabled
    {
        get => _disabled;
        private set => Set(ref _disabled, value);
    }

    internal void RefreshDisabled() => Disabled = Display.Removed;

    public RichString ToolTip => Display.ToolTip;

    internal void RaiseToolTipIfHealthChanged()
    {
        float health = Display.Health;

        if (health == _toolTipHealth) { return; }

        _toolTipHealth = health;

        Raise(nameof(ToolTip));
    }
}

internal sealed class CharacterBrowserViewModel : Notifiable
{
    private readonly IDialogHost _host;
    private readonly HashSet<ContentPackage> _packages = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Identifier> _species = [];
    private readonly HashSet<CharacterTeamType> _teams = [];
    private readonly List<CharacterTileViewModel> _rows = [];
    private readonly List<SpeciesOption> _speciesOptions = [];
    private readonly List<TeamOption> _teamOptions = [];
    private readonly Dictionary<Character, CharacterDisplay> _displays = new(ReferenceEqualityComparer.Instance);

    private IReadOnlyList<CharacterTileViewModel> _visible = [];
    private IReadOnlyList<PackageOption> _packageOptions = [];
    private Action<Character> _onPicked = static _ => { };
    private string _query = string.Empty;
    private float _scroll;
    private int _packageSignature;

    internal CharacterBrowserViewModel(IDialogHost host)
    {
        _host = host;

        PickPackagesCommand = new RelayCommand(PickPackages);
        PickSpeciesCommand = new RelayCommand(PickSpecies);
        PickTeamsCommand = new RelayCommand(PickTeams);
    }

    public LocalizedString Title => TextManager.Get("sandboxmenu.characters.title");

    public LocalizedString PackageFilter => FilterLabel("sandboxmenu.filter.packages", _packages.Count);

    public LocalizedString SpeciesFilter => FilterLabel("sandboxmenu.filter.species", _species.Count);

    public LocalizedString TeamFilter => FilterLabel("sandboxmenu.filter.teams", _teams.Count);

    public LocalizedString EmptyHint => TextManager.Get("sandboxmenu.characters.empty");

    public bool Empty => _visible.Count == 0;

    public RelayCommand PickPackagesCommand { get; }

    public RelayCommand PickSpeciesCommand { get; }

    public RelayCommand PickTeamsCommand { get; }

    public IReadOnlyList<CharacterTileViewModel> Rows => _visible;

    public string Query
    {
        get => _query;
        set
        {
            if (Set(ref _query, value ?? string.Empty)) { ApplyFilter(); }
        }
    }

    public float Scroll
    {
        get => _scroll;
        set => Set(ref _scroll, value);
    }

    internal void PickInto(Action<Character> onPicked) => _onPicked = onPicked;

    internal void RefreshStates()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            _rows[i].RefreshDisabled();
            _rows[i].RaiseToolTipIfHealthChanged();
        }
    }

    internal void Refresh()
    {
        _rows.Clear();
        _speciesOptions.Clear();
        _teamOptions.Clear();

        HashSet<Identifier> species = [];
        HashSet<CharacterTeamType> teams = [];

        foreach (Character character in Character.CharacterList)
        {
            if (character is null || character.Removed) { continue; }

            if (!_displays.TryGetValue(character, out CharacterDisplay? display))
            {
                _displays[character] = display = new CharacterDisplay(character);
            }

            _rows.Add(new CharacterTileViewModel(display, Picked));

            if (species.Add(character.SpeciesName)) { _speciesOptions.Add(new SpeciesOption(character.SpeciesName, display.Species)); }
            if (teams.Add(character.TeamID)) { _teamOptions.Add(new TeamOption(character.TeamID, display.Team)); }
        }

        _rows.Sort(static (a, b) => string.Compare(a.Display.Name.Value, b.Display.Name.Value, StringComparison.OrdinalIgnoreCase));

        _speciesOptions.Sort(static (a, b) => string.Compare(a.Label.Value, b.Label.Value, StringComparison.OrdinalIgnoreCase));
        _teamOptions.Sort(static (a, b) => string.Compare(a.Label.Value, b.Label.Value, StringComparison.OrdinalIgnoreCase));

        int packageSignature = _rows.Count;

        for (int i = 0; i < _rows.Count; i++)
        {
            packageSignature = packageSignature * 31 + RuntimeHelpers.GetHashCode(_rows[i].Display.Package);
        }

        if (packageSignature != _packageSignature)
        {
            _packageSignature = packageSignature;
            _packageOptions = PackageOptions.Build(_rows.Select(row => row.Display.Package));
        }

        _packages.Clear();
        _species.Clear();
        _teams.Clear();

        ApplyFilter();
    }

    internal void Release()
    {
        _rows.Clear();
        _speciesOptions.Clear();
        _teamOptions.Clear();
        _packageOptions = [];
        _visible = [];
        _displays.Clear();
        _packageSignature = 0;

        _packages.Clear();
        _species.Clear();
        _teams.Clear();

        Raise(nameof(Rows));
        Raise(nameof(Empty));
    }

    private void Picked(Character character) => _onPicked(character);

    private void PickPackages()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.filter.packages"),
            _packageOptions.Select(option => new PickerToggle(
                RichString.Rich(Theme.AccentMarkup(option.Label, option.Package)),
                () => _packages.Contains(option.Package),
                selected => SetPackage(option.Package, selected))));

    private void PickSpecies()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.filter.species"),
            _speciesOptions.Select(option => new PickerToggle(
                option.Label,
                () => _species.Contains(option.Species),
                selected => SetSpecies(option.Species, selected))));

    private void PickTeams()
        => _host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.filter.teams"),
            _teamOptions.Select(option => new PickerToggle(
                option.Label,
                () => _teams.Contains(option.Team),
                selected => SetTeam(option.Team, selected))));

    private void SetPackage(ContentPackage package, bool selected)
    {
        if (selected) { _packages.Add(package); }
        else { _packages.Remove(package); }

        ApplyFilter();
    }

    private void SetSpecies(Identifier species, bool selected)
    {
        if (selected) { _species.Add(species); }
        else { _species.Remove(species); }

        ApplyFilter();
    }

    private void SetTeam(CharacterTeamType team, bool selected)
    {
        if (selected) { _teams.Add(team); }
        else { _teams.Remove(team); }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (NotificationBatch.Defer(this, nameof(ApplyFilter), RenewFilter)) { return; }

        RenewFilter();
    }

    private void RenewFilter()
    {
        string query = _query.Trim();
        List<CharacterTileViewModel> visible = new(_rows.Count);

        for (int i = 0; i < _rows.Count; i++)
        {
            CharacterTileViewModel row = _rows[i];

            if (Shows(row.Display, query)) { visible.Add(row); }
        }

        _visible = visible;

        using (NotificationBatch.Begin())
        {
            _scroll = 0f;
            Raise(nameof(Scroll));
            Raise(nameof(Rows));
            Raise(nameof(Empty));
            Raise(nameof(PackageFilter));
            Raise(nameof(SpeciesFilter));
            Raise(nameof(TeamFilter));
        }
    }

    private bool Shows(CharacterDisplay display, string query)
    {
        Character character = display.Character;

        return (_packages.Count == 0 || (character.ContentPackage is { } package && _packages.Contains(package)))
            && (_species.Count == 0 || _species.Contains(character.SpeciesName))
            && (_teams.Count == 0 || _teams.Contains(character.TeamID))
            && (query.Length == 0 || Matches(display, query));
    }

    private static bool Matches(CharacterDisplay display, string query)
        => display.Name.Value.Contains(query, StringComparison.OrdinalIgnoreCase)
            || display.Identifier.Contains(query, StringComparison.OrdinalIgnoreCase)
            || display.Character.Group.Value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static LocalizedString FilterLabel(string nameKey, int chosen)
    {
        LocalizedString count = chosen.ToString();

        return TextManager.GetWithVariables(
            chosen == 0 ? "sandboxmenu.filter.summary.all" : "sandboxmenu.filter.summary.some",
            ("[name]", TextManager.Get(nameKey)),
            ("[count]", count));
    }
}
