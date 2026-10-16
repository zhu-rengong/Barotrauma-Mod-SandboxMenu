namespace SandboxMenu.UI.ViewModels;

internal sealed class ToggleRowViewModel(PickerToggle option) : PickerRowViewModel
{
    public RichString Label { get; } = option.Label;

    public Sprite? Icon { get; } = option.Icon;

    public bool Selected
    {
        get => option.IsTicked();
        set => option.Toggled(value);
    }

    internal void Tick(bool ticked)
    {
        if (option.IsTicked() == ticked) { return; }

        option.Toggled(ticked);
        Raise(nameof(Selected));
    }
}
