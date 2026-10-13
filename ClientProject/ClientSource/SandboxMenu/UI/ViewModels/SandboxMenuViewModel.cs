using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.ViewModels;

internal sealed class SandboxMenuViewModel : Notifiable, IItemDropTarget, IListBackground
{
    private LocalizedString _status = LocalizedString.EmptyString;
    private StatusKind _statusKind;
    private bool _statusPulse;

    internal SandboxMenuViewModel(IDialogHost host)
    {
        Spawn = new SpawnPanelViewModel(host, Report);
        Afflictions = new AfflictionPanelViewModel(host, Report);
    }

    public FunctionSelectorViewModel Functions { get; } = new();

    public SpawnPanelViewModel Spawn { get; }

    public AfflictionPanelViewModel Afflictions { get; }

    public LocalizedString Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public StatusKind StatusKind
    {
        get => _statusKind;
        private set => Set(ref _statusKind, value);
    }

    public bool StatusPulse
    {
        get => _statusPulse;
        private set => Set(ref _statusPulse, value);
    }

    public bool CanDrag(object item) => Spawn.CanDrag(item);

    public bool CanNest(object target) => Spawn.CanNest(target);

    public void Drop(object source, object? target, DropMode mode) => Spawn.Drop(source, target, mode);

    public void ShowBackgroundMenu(Vector2 position) => Spawn.ShowBackgroundMenu(position);

    internal void Report(LocalizedString message)
    {
        StatusKind = StatusTones.Of(message);
        StatusPulse = !_statusPulse;
        Status = message;
    }

    internal void RefreshStatusKind() => StatusKind = StatusTones.Of(_status);
}
