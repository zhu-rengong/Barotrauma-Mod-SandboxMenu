namespace SandboxMenu.UI.ViewModels;

internal sealed class ToggleRowViewModel(PickerToggle option) : PickerRow
{
    public RichString Label { get; } = option.Label;

    public Sprite? Icon { get; } = option.Icon;

    public bool Selected
    {
        get => option.IsTicked();
        set => option.Toggled(value);
    }

    // The bound tick box only takes its value again when it is told, so the row has to say it changed.
    internal void Tick(bool ticked)
    {
        // Writing the option runs the picker's filter again, so an already-ticked row is left alone.
        if (option.IsTicked() == ticked) { return; }

        option.Toggled(ticked);
        Raise(nameof(Selected));
    }
}
