namespace SandboxMenu.UI.Models;

internal sealed class AfflictionDisplay
{
    private static readonly Dictionary<AfflictionPrefab, AfflictionDisplay> _cache = new(ReferenceEqualityComparer.Instance);

    private readonly AfflictionPrefab _prefab;
    private readonly LocalizedString _name;

    private AfflictionDisplay(AfflictionPrefab prefab, string identifier, LocalizedString name)
    {
        _prefab = prefab;
        Identifier = identifier;
        _name = name;
    }

    internal AfflictionPrefab Prefab => _prefab;

    public string Identifier { get; }

    public LocalizedString Name => _name;

    public Sprite? Icon => _prefab.Icon;

    public bool IsBuff => _prefab.IsBuff;

    public bool LimbSpecific => _prefab.LimbSpecific;

    public float MaxStrength => _prefab.MaxStrength > 0f ? _prefab.MaxStrength : 1f;

    public RichString Title => RichString.Rich(TooltipText.Bright(_name) + " " + TooltipText.Dim(Identifier));

    public Color ColorAt(float strength)
        => _prefab.Icon is null ? Color.White : CharacterHealth.GetAfflictionIconColor(_prefab, MathHelper.Clamp(strength, 0f, MaxStrength));

    public LocalizedString Description(float strength)
        => _prefab.GetDescription(MathHelper.Clamp(strength, 0f, MaxStrength), AfflictionPrefab.Description.TargetType.OtherCharacter);

    public RichString ToolTip()
        => RichString.Rich(TitleText + "\n" + Details + DescriptionsText + PackageText + ApplyHint);

    internal static AfflictionDisplay For(AfflictionPrefab prefab)
    {
        if (_cache.TryGetValue(prefab, out AfflictionDisplay? built)) { return built; }

        AfflictionDisplay display = Build(prefab);
        _cache[prefab] = display;

        return display;
    }

    internal static AfflictionDisplay? For(string? identifier)
        => AfflictionPrefabLookup.By(identifier) is { } prefab ? For(prefab) : null;

    internal static void Reset() => _cache.Clear();

    private LocalizedString TitleText => TooltipText.Bright(_name) + " " + TooltipText.Dim(Identifier);

    public LocalizedString Details
    {
        get
        {
            LocalizedString text = LocalizedString.EmptyString;

            if (!_prefab.AfflictionType.IsEmpty)
            {
                text = TooltipText.Colorize("[" + _prefab.AfflictionType.Value + "]", Theme.TagText) + " ";
            }

            text += TooltipText.Colored(
                _prefab.LimbSpecific ? "sandboxmenu.afflictions.field.limbspecific" : "sandboxmenu.afflictions.field.body",
                GUIStyle.Orange);

            return text + " " + (_prefab.IsBuff
                ? TooltipText.Colored("sandboxmenu.afflictions.field.buff", Theme.BuffText)
                : TooltipText.Colored("sandboxmenu.afflictions.field.debuff", GUIStyle.Red));
        }
    }

    private LocalizedString DescriptionsText
    {
        get
        {
            if (_prefab.Descriptions is not { Count: > 0 } descriptions)
            {
                return _prefab.GetDescription(0f, AfflictionPrefab.Description.TargetType.OtherCharacter);
            }

            LocalizedString text = LocalizedString.EmptyString;

            foreach (AfflictionPrefab.Description description in descriptions)
            {
                if (description.Target == AfflictionPrefab.Description.TargetType.Self) { continue; }
                if (description.Text.IsNullOrWhiteSpace()) { continue; }

                text += "\n" + TooltipText.Colorize(
                    "[" + TooltipText.Number(description.MinStrength) + "-" + TooltipText.Number(description.MaxStrength) + "]",
                    Theme.TagText) + " " + description.Text;
            }

            return text;
        }
    }

    private LocalizedString PackageText
        => _prefab.ContentPackage is { } package ? "\n" + Theme.AccentMarkup(package.Name, package) : LocalizedString.EmptyString;

    private LocalizedString ApplyHint
        => "\n" + TooltipText.Colorize(TextManager.Get("sandboxmenu.afflictions.apply.hint"), Theme.Callout);

    private static AfflictionDisplay Build(AfflictionPrefab prefab)
    {
        string identifier = prefab.Identifier.Value;

        LocalizedString? own = prefab.Name;
        LocalizedString name = string.IsNullOrEmpty(own?.Value) ? identifier : own;

        return new AfflictionDisplay(prefab, identifier, name);
    }
}
