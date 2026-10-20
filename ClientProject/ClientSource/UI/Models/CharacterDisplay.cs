using System.Runtime.CompilerServices;

namespace SandboxMenu.UI.Models;

public sealed class CharacterDisplay(Character character)
{
    internal const float VisibleStrength = 0.05f;

    private static readonly LimbType[] PreferredLimbs =
    [
        LimbType.Torso, LimbType.Head, LimbType.Waist,
        LimbType.LeftArm, LimbType.RightArm,
        LimbType.LeftLeg, LimbType.RightLeg,
        LimbType.LeftForearm, LimbType.RightForearm,
        LimbType.LeftThigh, LimbType.RightThigh,
        LimbType.LeftHand, LimbType.RightHand,
        LimbType.LeftFoot, LimbType.RightFoot,
        LimbType.Legs, LimbType.Tail, LimbType.Jaw, LimbType.None
    ];

    private static readonly Dictionary<LimbType, int> LimbRanks = BuildRanks();

    private readonly Dictionary<int, Limb> _limbIndex = [];

    private RichString? _toolTip;
    private float _toolTipHealth = float.NaN;

    private CharacterHealth? _afflictionsFrom;
    private IReadOnlyCollection<Affliction> _afflictions = [];
    private int _limbSignature;
    private bool _limbsBuilt;
    private IReadOnlyList<Limb> _limbs = [];

    internal readonly record struct AfflictionGroup(LocalizedString Label, Limb? Limb, IReadOnlyList<Affliction> Items);

    internal Character Character => character;

    internal bool Removed => character.Removed;

    internal CharacterInfo? Info => character.Info;

    internal CharacterPreview Preview => CharacterPreview.For(character);

    internal CharacterPreview PreviewHead => CharacterPreview.For(character, CharacterPreviewFraming.Head);

    internal bool HasJob => character.Info?.Job is not null;

    internal LocalizedString Name => character.DisplayName;

    internal string Identifier => character.Prefab.Identifier.Value;

    internal LocalizedString Species
    {
        get
        {
            string own = character.Params.DisplayName;

            if (!string.IsNullOrWhiteSpace(own)) { return own; }

            Identifier key = character.Params.SpeciesTranslationOverride.IsEmpty
                ? character.SpeciesName
                : character.Params.SpeciesTranslationOverride;

            LocalizedString translated = TextManager.Get("Character." + key);

            return translated.IsNullOrWhiteSpace() ? character.SpeciesName.Value : translated;
        }
    }

    internal LocalizedString Group => character.Group.IsEmpty ? TextManager.Get("sandboxmenu.afflictions.field.none") : character.Group.Value;

    internal LocalizedString JobName => character.Info?.Job?.Name ?? LocalizedString.EmptyString;

    internal string JobIdentifier => character.Info?.Job?.Prefab.Identifier.Value ?? string.Empty;

    internal Sprite? JobIcon => character.Info?.Job?.Prefab.Icon;

    internal Sprite? JobSmallIcon => character.Info?.Job?.Prefab.IconSmall ?? JobIcon;

    internal Color JobColor => character.Info?.Job?.Prefab.UIColor ?? Color.White;

    internal LocalizedString Team => TeamName(character.TeamID);

    internal float Health => character.Vitality;

    internal float MaxHealth => character.MaxVitality;

    internal float HealthPercentage => character.HealthPercentage;

    internal ContentPackage? Package => character.ContentPackage;

    internal string PackageKey => PackageLabels.KeyOf(Package);

    internal CharacterHealth? CharacterHealth => character.CharacterHealth;

    internal string HealthText
        => TooltipText.Number(Health) + " / " + TooltipText.Number(MaxHealth)
            + " (" + TooltipText.Number(MathF.Round(HealthPercentage), "0") + "%)";

    internal RichString ToolTip
    {
        get
        {
            float health = Health;

            if (_toolTip is { } cached && health == _toolTipHealth) { return cached; }

            _toolTipHealth = health;

            return _toolTip = RichString.Rich(BuildToolTip());
        }
    }

    internal IReadOnlyCollection<Affliction> Afflictions()
    {
        if (character.CharacterHealth is not { } health) { return []; }

        if (!ReferenceEquals(_afflictionsFrom, health))
        {
            _afflictionsFrom = health;
            _afflictions = health.GetAllAfflictions();
        }

        return _afflictions;
    }

    internal IReadOnlyList<Limb> Limbs()
    {
        if (character.AnimController is not { } controller) { return []; }

        int signature = LimbSignature(controller);

        if (_limbsBuilt && signature == _limbSignature) { return _limbs; }

        _limbsBuilt = true;
        _limbSignature = signature;
        _limbIndex.Clear();

        foreach (Limb limb in controller.Limbs)
        {
            if (limb.HealthIndex < 0) { continue; }

            if (!_limbIndex.TryGetValue(limb.HealthIndex, out Limb? kept) || IsBetterName(limb, kept)) { _limbIndex[limb.HealthIndex] = limb; }
        }

        Limb[] limbs = [.. _limbIndex.Values];

        Array.Sort(limbs, static (left, right) => left.HealthIndex.CompareTo(right.HealthIndex));

        return _limbs = limbs;
    }

    internal IReadOnlyList<Limb> LimbsIn(IReadOnlySet<int> healthIndexes)
    {
        List<Limb> limbs = [];

        foreach (Limb limb in Limbs())
        {
            if (healthIndexes.Contains(limb.HealthIndex)) { limbs.Add(limb); }
        }

        return limbs;
    }

    internal int Shape()
    {
        if (character.CharacterHealth is not { } health) { return 0; }

        int hash = 17;

        foreach (Affliction affliction in Afflictions())
        {
            if (affliction.Strength < VisibleStrength) { continue; }

            hash = hash * 31 + RuntimeHelpers.GetHashCode(affliction);
            hash = hash * 31 + (health.GetAfflictionLimb(affliction)?.HealthIndex ?? -1);
        }

        return hash;
    }

    internal IReadOnlyList<AfflictionGroup> Groups(bool hideEmptyLimbs, bool sortByName)
    {
        List<AfflictionGroup> groups = [];

        if (character.CharacterHealth is not { } health) { return groups; }

        List<Affliction> general = [];
        Dictionary<int, List<Affliction>> byLimb = [];

        foreach (Affliction affliction in Afflictions())
        {
            if (affliction.Strength < VisibleStrength) { continue; }

            if (health.GetAfflictionLimb(affliction) is not { } limb) { general.Add(affliction); continue; }

            if (!byLimb.TryGetValue(limb.HealthIndex, out List<Affliction>? found)) { byLimb[limb.HealthIndex] = found = []; }

            found.Add(affliction);
        }

        Add(groups, null, TextManager.Get("sandboxmenu.afflictions.group.body"), general, hideEmptyLimbs, sortByName);

        foreach (Limb limb in Limbs())
        {
            byLimb.Remove(limb.HealthIndex, out List<Affliction>? found);
            Add(groups, limb, LimbLabel(limb), found, hideEmptyLimbs, sortByName);
        }

        foreach (KeyValuePair<int, List<Affliction>> pair in byLimb)
        {
            Add(groups, null, TextManager.Get("sandboxmenu.afflictions.group.other"), pair.Value, hideEmptyLimbs, sortByName);
        }

        return groups;
    }

    internal LocalizedString LimbNames(IEnumerable<Limb> limbs)
    {
        List<LocalizedString> labels = [];

        foreach (Limb limb in limbs) { labels.Add(LimbLabel(limb)); }

        return TooltipText.Join(TextManager.Get("sandboxmenu.list.separator"), labels);
    }

    internal static LocalizedString LimbLabel(Limb limb)
    {
        LocalizedString label = TextManager.Get("sandboxmenu.limb." + limb.type.ToString().ToLowerInvariant());

        return label.IsNullOrWhiteSpace() ? limb.Name : label;
    }

    internal static LocalizedString TeamName(CharacterTeamType team)
        => team switch
        {
            CharacterTeamType.Team1 or CharacterTeamType.Team2 => CombatMission.GetTeamName(team),
            CharacterTeamType.FriendlyNPC => TextManager.Get("sandboxmenu.team.friendlynpc"),
            _ => TextManager.Get("sandboxmenu.team.none")
        };

    private static void Add(
        List<AfflictionGroup> groups,
        Limb? limb,
        LocalizedString label,
        List<Affliction>? found,
        bool hideEmptyLimbs,
        bool sortByName)
    {
        int count = found?.Count ?? 0;

        if (count == 0 && hideEmptyLimbs && limb is not null) { return; }

        Sort(found, sortByName);

        groups.Add(new AfflictionGroup(label, limb, found is null ? [] : found));
    }

    private static void Sort(List<Affliction>? found, bool sortByName)
    {
        if (found is not { Count: > 1 }) { return; }

        AfflictionKey[] keys = new AfflictionKey[found.Count];

        for (int i = 0; i < found.Count; i++)
        {
            Affliction affliction = found[i];

            keys[i] = new AfflictionKey(
                affliction,
                sortByName ? AfflictionDisplay.For(affliction.Prefab).Name.Value : string.Empty,
                affliction.Strength);
        }

        if (sortByName) { Array.Sort(keys, static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)); }
        else { Array.Sort(keys, static (left, right) => right.Strength.CompareTo(left.Strength)); }

        for (int i = 0; i < keys.Length; i++) { found[i] = keys[i].Affliction; }
    }

    private static int LimbSignature(AnimController controller)
    {
        int hash = RuntimeHelpers.GetHashCode(controller);

        foreach (Limb limb in controller.Limbs)
        {
            hash = hash * 31 + limb.HealthIndex;
            hash = hash * 31 + (int)limb.type;
            hash = hash * 31 + (limb.Hidden ? 1 : 0);
        }

        return hash;
    }

    private static bool IsBetterName(Limb candidate, Limb current)
        => candidate.Hidden == current.Hidden ? Rank(candidate) < Rank(current) : !candidate.Hidden;

    private static int Rank(Limb limb) => LimbRanks.TryGetValue(limb.type, out int rank) ? rank : int.MaxValue;

    private static Dictionary<LimbType, int> BuildRanks()
    {
        Dictionary<LimbType, int> ranks = [];

        for (int i = 0; i < PreferredLimbs.Length; i++) { ranks.TryAdd(PreferredLimbs[i], i); }

        return ranks;
    }

    private LocalizedString BuildToolTip()
    {
        LocalizedString text = TooltipText.Bright(Name) + " " + TooltipText.Dim(Identifier);
        text += "\n" + TooltipText.Field("sandboxmenu.afflictions.field.species", Species);
        text += "\n" + TooltipText.Field("sandboxmenu.afflictions.field.group", Group);

        if (HasJob)
        {
            text += "\n" + TooltipText.Field("sandboxmenu.afflictions.field.job", JobName + " " + TooltipText.Dim(JobIdentifier));
        }

        text += "\n" + TooltipText.Field("sandboxmenu.afflictions.field.team", Team);
        text += "\n" + TooltipText.FieldText("sandboxmenu.afflictions.field.health", HealthText);

        if (Package is { } package) { text += "\n" + Theme.AccentMarkup(package.Name, package); }

        return text;
    }

    private readonly record struct AfflictionKey(Affliction Affliction, string Name, float Strength);
}
