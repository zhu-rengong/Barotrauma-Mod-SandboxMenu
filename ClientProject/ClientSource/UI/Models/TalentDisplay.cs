using System.Globalization;

namespace SandboxMenu.UI.Models;

internal sealed class TalentDisplay
{
    private static readonly Dictionary<TalentPrefab, TalentDisplay> _cache = new(ReferenceEqualityComparer.Instance);

    private readonly TalentPrefab _prefab;
    private readonly LocalizedString _name;

    private TalentDisplay(TalentPrefab prefab, string identifier, LocalizedString name)
    {
        _prefab = prefab;
        Identifier = identifier;
        _name = name;
    }

    internal TalentPrefab Prefab => _prefab;

    public string Identifier { get; }

    public LocalizedString Name => _name;

    public Sprite? Icon => _prefab.Icon;

    public bool Hidden => _prefab.IsHiddenExtraTalent;

    public Color OwnedColor => _prefab.ColorOverride.TryUnwrap(out Color color) ? color : GUIStyle.Green;

    public LocalizedString Description => _prefab.Description;

    internal RichString ToolTip(CharacterInfo? info, bool includeApplyHint = true)
    {
        LocalizedString description = RichString.Rich(_prefab.Description, ToolBox.ExtendColorToPercentageSigns);
        LocalizedString text = TooltipText.Bright(_name) + " " + TooltipText.Dim(Identifier) + "\n\n" + description;

        if (info is not null && _prefab.TrackedStat.TryUnwrap(out (Identifier PermanentStatIdentifier, int Max) stat))
        {
            float statValue = info.GetSavedStatValue(StatTypes.None, stat.PermanentStatIdentifier);
            int intValue = (int)MathF.Round(statValue);

            text += "\n\n" + (statValue < stat.Max
                ? TextManager.GetWithVariables(
                    "talentprogress",
                    ("[amount]", intValue.ToString(CultureInfo.InvariantCulture)),
                    ("[max]", stat.Max.ToString(CultureInfo.InvariantCulture)))
                : TextManager.Get("talentprogresscompleted"));
        }

        if (_prefab.ContentPackage is { } package) { text += "\n" + Theme.AccentMarkup(package.Name, package); }

        if (includeApplyHint) { text += "\n" + TooltipText.Colorize(TextManager.Get("sandboxmenu.talents.apply.hint"), Theme.Callout); }

        return RichString.Rich(text);
    }

    internal static TalentDisplay For(TalentPrefab prefab)
    {
        if (_cache.TryGetValue(prefab, out TalentDisplay? built)) { return built; }

        TalentDisplay display = Build(prefab);
        _cache[prefab] = display;

        return display;
    }

    internal static void Reset() => _cache.Clear();

    private static TalentDisplay Build(TalentPrefab prefab)
    {
        string identifier = prefab.Identifier.Value;
        LocalizedString own = prefab.DisplayName;
        LocalizedString name = string.IsNullOrEmpty(own.Value) ? identifier : own;

        return new TalentDisplay(prefab, identifier, name);
    }
}
