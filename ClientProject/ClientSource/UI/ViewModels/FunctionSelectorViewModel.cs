namespace SandboxMenu.UI.ViewModels;

internal enum FunctionTab
{
    Spawn,
    Afflictions,
    Talents
}

internal sealed class FunctionSelectorViewModel : Notifiable
{
    private FunctionTab _active = FunctionTab.Spawn;
    private FunctionTab? _shown = FunctionTab.Spawn;

    public IReadOnlyList<LocalizedString> Items =>
    [
        TextManager.Get("sandboxmenu.function.spawn"),
        TextManager.Get("sandboxmenu.function.afflictions"),
        TextManager.Get("sandboxmenu.function.talents")
    ];

    public int SelectedIndex
    {
        get => (int)_active;
        set
        {
            if (!Enum.IsDefined(typeof(FunctionTab), value) || value == (int)_active) { return; }

            _active = (FunctionTab)value;

            RaiseDirect(nameof(SelectedIndex));
            SetShown(null);
            SetShown(_active);
        }
    }

    public bool ShowSpawn => _shown == FunctionTab.Spawn;

    public bool ShowAfflictions => _shown == FunctionTab.Afflictions;

    public bool ShowTalents => _shown == FunctionTab.Talents;

    private void SetShown(FunctionTab? tab)
    {
        _shown = tab;

        RaiseDirect(nameof(ShowSpawn));
        RaiseDirect(nameof(ShowAfflictions));
        RaiseDirect(nameof(ShowTalents));
    }
}
