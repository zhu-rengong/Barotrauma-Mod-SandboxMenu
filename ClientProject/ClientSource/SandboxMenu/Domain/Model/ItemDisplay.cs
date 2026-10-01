using Barotrauma.Items.Components;

namespace SandboxMenu.Domain.Model;

internal sealed class ItemDisplay
{
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
        _tags = tags.Length == 0 ? string.Empty : $"‖color:{MenuTheme.TagText.ToStringHex()}‖{tags}‖color:end‖";
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

    // A tile shows the icon alone, so its hint carries what a row shows next to it: the name with the identifier, the
    // tags, and then the notes every item hint ends with. The text is held on the display (which is cached per prefab
    // and let go with the content), as the tiles ask for it again on every step the list scrolls; what the skill
    // requirements say depends on whoever is controlled at the moment, so that part joins the held text every time.
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

            // What wearing the item changes, from the prefab's own wearable modifiers, the way the game lists it in its
            // item hint: the host builds this part, so the wording, the order and the colors stay the game's own.
            if (_prefab.wearableDamageModifiers.Count > 0 || _prefab.wearableSkillModifiers.Count > 0)
            {
                // The host puts a break before each entry unless the text is blank, so a note-less item needs one
                // seeded here; a lone break still counts as blank, so an empty description is not broken twice.
                if (text.IsNullOrWhiteSpace()) { text = "\n"; }

                Wearable.AddTooltipInfo(_prefab.wearableDamageModifiers, _prefab.wearableSkillModifiers, ref text);
            }

            return text;
        }
    }

    // The requirements the game weighs against the skills of whoever is controlled, listed the way its own item hint
    // lists them; blank again when the item asks for no skill, or when nobody is being played to weigh them against.
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
    {
        get
        {
            if (_prefab.ContentPackage is { } package)
            {
                return "\n‖color:" + package.GetAccentColor().ToStringHex() + "‖" + package.Name + "‖color:end‖";
            }

            return LocalizedString.EmptyString;
        }
    }

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
