namespace SandboxMenu.UI.ViewModels;

internal sealed class AfflictionItemRowViewModel : Notifiable
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

    internal int? LimbHealthIndex => _limb?.HealthIndex;

    internal void Refresh()
    {
        float strength = _affliction.Strength;
        float impact = _health.GetVitalityDecreaseWithVitalityMultipliers(_affliction);

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
