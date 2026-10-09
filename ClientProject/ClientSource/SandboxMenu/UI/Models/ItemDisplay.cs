using Barotrauma.Items.Components;

namespace SandboxMenu.UI.Models;

internal sealed class ItemDisplay
{
    private static readonly Dictionary<ItemPrefab, ItemDisplay> _cache = new(ReferenceEqualityComparer.Instance);

    private readonly ItemPrefab _prefab;
    private readonly LocalizedString _name;
    private readonly LocalizedString? _description;
    private readonly string _tags;

    private LocalizedString? _tileToolTipText;
    private RichString? _tileToolTip;

    private ItemDisplay(ItemPrefab prefab, string identifier, LocalizedString name, LocalizedString? description, string tags, Sprite? icon)
    {
        _prefab = prefab;
        Identifier = identifier;
        _name = name;
        _description = description;
        _tags = tags.Length == 0 ? string.Empty : $"‖color:{Theme.TagText.ToStringHex()}‖{tags}‖color:end‖";
        Tags = RichString.Rich(_tags);
        Icon = icon;
    }

    internal ItemPrefab Prefab => _prefab;

    public string Identifier { get; }

    public LocalizedString Name => _name;

    public RichString Tags { get; }

    public Sprite? Icon { get; }

    public RichString Title => RichString.Rich(_name + IdentifierSuffix);

    internal RichString TitleFor(LocalizedString title) => RichString.Rich(title + IdentifierSuffix);

    private string IdentifierSuffix => $" ‖color:{UiMetrics.TextDim.ToStringHex()}‖{Identifier}‖color:end‖";

    public RichString ToolTip => RichString.Rich(ToolTipText + SkillHints + PackageText);

    public RichString TileToolTip
    {
        get
        {
            LocalizedString text = _tileToolTipText ??= TileToolTipText;
            LocalizedString hints = SkillHints;

            return hints.Value.Length == 0
                ? _tileToolTip ??= RichString.Rich(text + PackageText)
                : RichString.Rich(text + hints + PackageText);
        }
    }

    private LocalizedString TileToolTipText
    {
        get
        {
            LocalizedString text = "‖color:" + ((Color)GUIStyle.TextColorBright).ToStringHex() + "‖" + _name + IdentifierSuffix + "‖color:end‖";

            if (_tags.Length > 0) { text += "\n" + _tags; }

            return text + NotesText;
        }
    }

    private LocalizedString NotesText
    {
        get
        {
            LocalizedString text = LocalizedString.EmptyString;

            if (_description is { } description) { text += "\n" + description; }

            if (_prefab.wearableDamageModifiers.Count > 0 || _prefab.wearableSkillModifiers.Count > 0)
            {
                if (text.IsNullOrWhiteSpace()) { text = "\n"; }

                Wearable.AddTooltipInfo(_prefab.wearableDamageModifiers, _prefab.wearableSkillModifiers, ref text);
            }

            return text;
        }
    }

    private LocalizedString SkillHints
    {
        get
        {
            if (_prefab.SkillRequirementHints.IsDefaultOrEmpty || Character.Controlled is not { } player)
            {
                return LocalizedString.EmptyString;
            }

            return _prefab.GetSkillRequirementHints(player);
        }
    }

    private LocalizedString PackageText
        => _prefab.ContentPackage is { } package ? "\n" + Theme.AccentMarkup(package.Name, package) : LocalizedString.EmptyString;

    private LocalizedString ToolTipText
    {
        get
        {
            LocalizedString name = _prefab.Category.HasFlag(MapEntityCategory.Legacy)
                ? TextManager.GetWithVariable("legacyitemformat", "[name]", _name, FormatCapitals.No)
                : _name;

            return "‖color:" + ((Color)GUIStyle.TextColorBright).ToStringHex() + "‖" + name + "‖color:end‖" + NotesText;
        }
    }

    internal static ItemDisplay For(ItemPrefab prefab)
    {
        if (_cache.TryGetValue(prefab, out ItemDisplay? built)) { return built; }

        ItemDisplay display = Build(prefab);
        _cache[prefab] = display;

        return display;
    }

    private static ItemDisplay Build(ItemPrefab prefab)
    {
        string identifier = prefab.Identifier.Value;

        LocalizedString? own = prefab.Name;
        LocalizedString name = string.IsNullOrEmpty(own?.Value) ? identifier : own;

        string tags = prefab.Tags is null
            ? string.Empty
            : string.Join(", ", prefab.Tags.Select(tag => tag.Value).OrderBy(value => value, StringComparer.OrdinalIgnoreCase));

        return new ItemDisplay(prefab, identifier, name, prefab.Description, tags, prefab.InventoryIcon ?? prefab.Sprite);
    }

    internal static ItemDisplay? For(string identifier)
        => ItemPrefabLookup.By(identifier) is { } prefab ? For(prefab) : null;
}
