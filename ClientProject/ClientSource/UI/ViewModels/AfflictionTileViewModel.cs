namespace SandboxMenu.UI.ViewModels;

internal sealed class AfflictionTileViewModel : Notifiable
{
    private readonly Action<AfflictionTileViewModel> _onPicked;
    private RichString? _toolTip;
    private float _percent;

    internal AfflictionTileViewModel(AfflictionPrefabEntry entry, float percent, Action<AfflictionTileViewModel> onPicked)
    {
        Entry = entry;
        Display = AfflictionDisplay.For(entry.Prefab);
        _percent = percent;
        _onPicked = onPicked;
        PickCommand = new RelayCommand(() => onPicked(this));
    }

    internal AfflictionPrefabEntry Entry { get; }

    public AfflictionDisplay Display { get; }

    public Sprite? Icon => Display.Icon;

    public RichString Name => RichString.Rich(Display.Name);

    public Color IconColor => Display.ColorAt(Display.MaxStrength * Math.Clamp(_percent, 0f, 100f) / 100f);

    public RichString StrengthText
        => RichString.Rich(TooltipText.Number(Display.MaxStrength * Math.Clamp(_percent, 0f, 100f) / 100f) + "/" + TooltipText.Number(Display.MaxStrength));

    public RichString ToolTip => _toolTip ??= Display.ToolTip();

    public RelayCommand PickCommand { get; }

    internal void SetStrength(float percent)
    {
        if (Math.Abs(_percent - percent) < 0.5f) { return; }

        _percent = percent;

        if (Display.Icon is not null) { Raise(nameof(IconColor)); }

        Raise(nameof(StrengthText));
    }
}
