namespace SandboxMenu.UI.ViewModels;

internal abstract class PickerRow : Notifiable
{
    private bool _visible = true;

    public bool Visible
    {
        get => _visible;
        set => Set(ref _visible, value);
    }
}
