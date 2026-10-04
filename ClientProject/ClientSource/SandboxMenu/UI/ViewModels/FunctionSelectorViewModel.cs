namespace SandboxMenu.UI.ViewModels;

internal enum MenuFunction
{
    Spawn,
    Afflictions
}

internal sealed class FunctionTabViewModel(string labelKey, Action onSelected) : Notifiable
{
    private bool _isActive;

    public LocalizedString Label => TextManager.Get(labelKey);

    public RelayCommand SelectCommand { get; } = new(onSelected);

    public bool IsActive
    {
        get => _isActive;
        internal set => Set(ref _isActive, value);
    }
}

// Which function area the main window shows: the tabs are the entries the title band offers, and the two flags
// decide which area's blocks take the room under the title.
internal sealed class FunctionSelectorViewModel : Notifiable
{
    private MenuFunction _active = MenuFunction.Spawn;

    internal FunctionSelectorViewModel()
    {
        SpawnTab = new FunctionTabViewModel("sandboxmenu.function.spawn", () => Select(MenuFunction.Spawn));
        AfflictionsTab = new FunctionTabViewModel("sandboxmenu.function.afflictions", () => Select(MenuFunction.Afflictions));

        SpawnTab.IsActive = true;
    }

    public FunctionTabViewModel SpawnTab { get; }

    public FunctionTabViewModel AfflictionsTab { get; }

    internal MenuFunction Active => _active;

    public bool ShowSpawn => _active == MenuFunction.Spawn;

    public bool ShowAfflictions => _active == MenuFunction.Afflictions;

    internal void Select(MenuFunction function)
    {
        if (_active == function) { return; }

        _active = function;
        SpawnTab.IsActive = _active == MenuFunction.Spawn;
        AfflictionsTab.IsActive = _active == MenuFunction.Afflictions;

        Raise(nameof(ShowSpawn));
        Raise(nameof(ShowAfflictions));
    }
}
