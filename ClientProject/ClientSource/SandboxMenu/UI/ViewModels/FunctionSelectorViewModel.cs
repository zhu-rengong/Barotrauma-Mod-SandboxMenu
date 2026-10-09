namespace SandboxMenu.UI.ViewModels;

internal enum FunctionTab
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

internal sealed class FunctionSelectorViewModel : Notifiable
{
    private FunctionTab _active = FunctionTab.Spawn;

    internal FunctionSelectorViewModel()
    {
        SpawnTab = new FunctionTabViewModel("sandboxmenu.function.spawn", () => Select(FunctionTab.Spawn));
        AfflictionsTab = new FunctionTabViewModel("sandboxmenu.function.afflictions", () => Select(FunctionTab.Afflictions));

        SpawnTab.IsActive = true;
    }

    public FunctionTabViewModel SpawnTab { get; }

    public FunctionTabViewModel AfflictionsTab { get; }

    internal FunctionTab Active => _active;

    public bool ShowSpawn => _active == FunctionTab.Spawn;

    public bool ShowAfflictions => _active == FunctionTab.Afflictions;

    internal void Select(FunctionTab function)
    {
        if (_active == function) { return; }

        _active = function;
        SpawnTab.IsActive = _active == FunctionTab.Spawn;
        AfflictionsTab.IsActive = _active == FunctionTab.Afflictions;

        Raise(nameof(ShowSpawn));
        Raise(nameof(ShowAfflictions));
    }
}
