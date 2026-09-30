namespace SandboxMenu.Domain.Model;

internal sealed class ItemDisplay
{
    private readonly ItemPrefab _prefab;
    private readonly LocalizedString _name;
    private readonly LocalizedString? _description;

    private ItemDisplay(ItemPrefab prefab, string identifier, LocalizedString name, LocalizedString? description, string tags, Sprite? icon)
    {
        _prefab = prefab;
        Identifier = identifier;
        _name = name;
        _description = description;
        Tags = tags.Length == 0
            ? RichString.Rich(string.Empty)
            : RichString.Rich($"‖color:{MenuTheme.TagText.ToStringHex()}‖{tags}‖color:end‖");
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

    public RichString ToolTip => RichString.Rich(ToolTipText);

    private LocalizedString ToolTipText
    {
        get
        {
            LocalizedString name = _prefab.Category.HasFlag(MapEntityCategory.Legacy)
                ? TextManager.GetWithVariable("legacyitemformat", "[name]", _name, FormatCapitals.No)
                : _name;

            LocalizedString text = "‖color:" + ((Color)GUIStyle.TextColorBright).ToStringHex() + "‖" + name + "‖color:end‖";

            if (_description is { } description) { text += "\n" + description; }

            if (_prefab.ContentPackage is { } package)
            {
                text += "\n‖color:" + package.GetAccentColor().ToStringHex() + "‖" + package.Name + "‖color:end‖";
            }

            return text;
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
