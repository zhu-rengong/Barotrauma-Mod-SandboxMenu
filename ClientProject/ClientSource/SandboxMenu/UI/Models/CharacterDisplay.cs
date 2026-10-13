namespace SandboxMenu.UI.Models;

public sealed class CharacterDisplay(Character character)
{
    private RichString? _toolTip;
    private float _toolTipHealth = float.NaN;

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

    internal static LocalizedString TeamName(CharacterTeamType team)
        => team switch
        {
            CharacterTeamType.Team1 or CharacterTeamType.Team2 => CombatMission.GetTeamName(team),
            CharacterTeamType.FriendlyNPC => TextManager.Get("sandboxmenu.team.friendlynpc"),
            _ => TextManager.Get("sandboxmenu.team.none")
        };

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
}
