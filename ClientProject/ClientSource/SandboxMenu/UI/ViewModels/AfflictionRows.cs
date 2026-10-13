namespace SandboxMenu.UI.ViewModels;

internal enum AfflictionSort
{
    Strength,
    Name
}

internal abstract class AfflictionRowViewModel : Notifiable
{
}

internal sealed class AfflictionGroupRowViewModel : AfflictionRowViewModel
{
    private int _count;

    internal AfflictionGroupRowViewModel(LocalizedString label) => Label = label;

    public LocalizedString Label { get; }

    public RichString Title => Count == 0
        ? RichString.Rich(Label)
        : RichString.Rich(Label + " " + TooltipText.Dim(Count.ToString()));

    public bool HasAfflictions => _count > 0;

    public int Count
    {
        get => _count;
        internal set
        {
            if (!Set(ref _count, value)) { return; }

            Raise(nameof(Title));
            Raise(nameof(HasAfflictions));
        }
    }
}

internal sealed class AfflictionItemRowViewModel : AfflictionRowViewModel
{
    private const float Tolerance = 0.05f;

    private readonly Affliction _affliction;
    private readonly CharacterHealth _health;
    private readonly Limb? _limb;

    private RichString _subText = string.Empty;
    private RichString _toolTip = string.Empty;
    private Color _iconColor = Color.White;
    private float _strength;
    private float _percent;
    private float _impact;
    private bool _measured;
    private bool _selected;

    internal AfflictionItemRowViewModel(
        Affliction affliction,
        CharacterHealth health,
        Limb? limb,
        Action<AfflictionItemRowViewModel> onSelected,
        Action<AfflictionItemRowViewModel> onMenu)
    {
        _affliction = affliction;
        _health = health;
        _limb = limb;
        Display = AfflictionDisplay.For(affliction.Prefab);

        SelectCommand = new RelayCommand(() => onSelected(this));
        MenuCommand = new RelayCommand(() => onMenu(this));
    }

    public AfflictionDisplay Display { get; }

    public RelayCommand SelectCommand { get; }

    public RelayCommand MenuCommand { get; }

    public bool Selected
    {
        get => _selected;
        internal set => Set(ref _selected, value);
    }

    public Sprite? Icon => Display.Icon;

    public LocalizedString Name => Display.Name;

    public RichString Title => RichString.Rich(Name + " " + TooltipText.Dim(Display.Identifier));

    public RichString SubText
    {
        get => _subText;
        private set => Set(ref _subText, value);
    }

    public RichString ToolTip
    {
        get => _toolTip;
        private set => Set(ref _toolTip, value);
    }

    public Color IconColor
    {
        get => _iconColor;
        private set => Set(ref _iconColor, value);
    }

    internal float Strength => _strength;

    internal Affliction Affliction => _affliction;

    internal AfflictionPrefab Prefab => _affliction.Prefab;

    internal int HealthIndex => _limb?.HealthIndex ?? -1;

    internal void Refresh()
    {
        float strength = _affliction.Strength;
        float impact = _affliction.GetVitalityDecrease(_health);

        if (_measured && Math.Abs(strength - _strength) < Tolerance && Math.Abs(impact - _impact) < Tolerance) { return; }

        _measured = true;
        _strength = strength;
        _percent = strength / Display.MaxStrength * 100f;
        _impact = impact;

        IconColor = Display.ColorAt(strength);
        SubText = BuildSubText();
        ToolTip = BuildToolTip();
    }

    private RichString BuildSubText()
        => RichString.Rich(_impact > 0f ? ValueText + "  " + ColorizedImpact : ValueText);

    private RichString BuildToolTip()
    {
        LocalizedString text = TooltipText.Bright(Name) + " " + TooltipText.Dim(Display.Identifier);
        text += "\n" + Display.Details;

        LocalizedString description = Display.Description(_strength);

        if (!description.IsNullOrWhiteSpace()) { text += "\n" + description; }
        if (Display.Prefab.ContentPackage is { } package) { text += "\n" + Theme.AccentMarkup(package.Name, package); }

        return RichString.Rich(text);
    }

    private string ValueText => TooltipText.Number(_strength) + " (" + TooltipText.Number(MathF.Round(_percent), "0") + "%)";

    private string ImpactText => "-" + TooltipText.Number(_impact);

    private LocalizedString ColorizedImpact
        => TooltipText.Colorize(ImpactText, _impact > 0f ? ImpactLossColor : GUIStyle.Green);

    private Color ImpactLossColor
        => Color.Lerp(GUIStyle.Orange, GUIStyle.Red, _percent / 100f);
}

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
