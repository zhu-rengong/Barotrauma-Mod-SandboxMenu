namespace SandboxMenu.UI.ViewModels;

internal enum FunctionTab
{
    Spawn,
    Afflictions
}

internal sealed class FunctionSelectorViewModel : Notifiable
{
    private FunctionTab _active = FunctionTab.Spawn;

    public IReadOnlyList<LocalizedString> Items =>
    [
        TextManager.Get("sandboxmenu.function.spawn"),
        TextManager.Get("sandboxmenu.function.afflictions")
    ];

    public int SelectedIndex
    {
        get => (int)_active;
        set
        {
            if (!Enum.IsDefined(typeof(FunctionTab), value) || value == (int)_active) { return; }

            _active = (FunctionTab)value;

            Raise(nameof(SelectedIndex));
            Raise(nameof(ShowSpawn));
            Raise(nameof(ShowAfflictions));
        }
    }

    public bool ShowSpawn => _active == FunctionTab.Spawn;

    public bool ShowAfflictions => _active == FunctionTab.Afflictions;
}
