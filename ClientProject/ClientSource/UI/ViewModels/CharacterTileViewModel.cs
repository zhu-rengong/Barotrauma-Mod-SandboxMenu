namespace SandboxMenu.UI.ViewModels;

internal sealed class CharacterTileViewModel(CharacterDisplay display, Action<Character> onPicked) : Notifiable
{
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
}
