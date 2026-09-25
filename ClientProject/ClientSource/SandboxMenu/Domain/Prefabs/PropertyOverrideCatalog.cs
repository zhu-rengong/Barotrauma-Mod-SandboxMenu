using System.Collections.Frozen;
using System.Globalization;
using Barotrauma.Items.Components;

namespace SandboxMenu.Domain.Prefabs;

internal readonly record struct OverrideTarget(string ComponentName, int ComponentIndex, LocalizedString Label);

internal sealed record PropertyOption(string Name, string TypeName, string DefaultValue)
{
    public string Label => $"{Name} ({TypeName}) = {DefaultValue}";
}

internal static class PropertyOverrideCatalog
{
    private static readonly Dictionary<Type, ImmutableArray<PropertyOption>> PropertyCache = [];

    private static FrozenDictionary<string, Type>? _componentTypes;

    static PropertyOverrideCatalog() => StaticState.Register(Clear);

    internal static void Clear()
    {
        PropertyCache.Clear();
        _componentTypes = null;
    }

    // A component name occurring more than once is numbered, which is the index the override needs to pick
    // the right one; a name occurring once stays plain.
    internal static IReadOnlyList<OverrideTarget> Targets(string identifier) =>
        DescribeTargets((identifier ?? string.Empty).Trim());

    internal static IReadOnlyList<PropertyOption> Properties(string identifier, string componentName, int componentIndex)
    {
        ResolvedTarget target = Resolve(identifier, componentName, componentIndex);
        if (target.Type is not { } type) { return []; }

        if (!PropertyCache.TryGetValue(type, out ImmutableArray<PropertyOption> declared))
        {
            declared = [.. DescribeProperties(type)];
            PropertyCache[type] = declared;
        }

        // A value the prefab declares for a property wins over the property's own default, which is what the item
        // is really created with; the cached table stays per type.
        return target.Element is not { } element
            ? declared
            : [.. declared.Select(option => AsDeclaredBy(option, element))];
    }

    private static PropertyOption AsDeclaredBy(PropertyOption option, ContentXElement element)
        => element.GetAttribute(option.Name)?.Value is { } value
            ? option with { DefaultValue = value }
            : option;

    private static List<OverrideTarget> DescribeTargets(string identifier)
    {
        List<OverrideTarget> targets = [ItemItself];

        if (!TryGetPrefab(identifier, out ItemPrefab prefab) || prefab.ConfigElement is null) { return targets; }

        List<ContentXElement> components = [.. prefab.ConfigElement.Elements().Where(IsComponent)];

        Dictionary<string, int> total = new(StringComparer.OrdinalIgnoreCase);
        foreach (ContentXElement component in components)
        {
            string name = component.Name.LocalName;
            total[name] = total.GetValueOrDefault(name) + 1;
        }

        Dictionary<string, int> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (ContentXElement component in components)
        {
            string name = component.Name.LocalName;
            int index = seen[name] = seen.GetValueOrDefault(name) + 1;

            targets.Add(new OverrideTarget(name, index, total[name] > 1 ? $"{name} [{index}]" : name));
        }

        return targets;
    }

    private static List<PropertyOption> DescribeProperties(Type type)
    {
        // The editor only ever has a prefab, never a live item, so the properties cannot be read off an instance.
        // An uninitialized one is enough: the engine reads nothing but the type and the attributes of each
        // property when it builds the table.
        object probe = RuntimeHelpers.GetUninitializedObject(type);
        List<PropertyOption> options = [];

        foreach (SerializableProperty property in SerializableProperty.GetProperties(probe).Values)
        {
            // Only what the game would load from XML and can parse back from a string: an override is applied
            // through SerializableProperty.TrySetValue(object, string).
            if (property.Attributes.OfType<Serialize>().FirstOrDefault() is not { } serialize) { continue; }
            if (SerializableProperty.GetSupportedTypeName(property.PropertyType) is not { } typeName) { continue; }

            string kind = string.Equals(typeName, "Enum", StringComparison.Ordinal) ? property.PropertyType.Name : typeName;

            // A default computed from the entity (CustomDefaultValueAttribute) cannot be evaluated without one, so
            // the value the attribute declares is all that can be shown.
            options.Add(new PropertyOption(property.Name, kind, FormatDefault(serialize.DefaultValue)));
        }

        options.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return options;
    }

    // The conversion is the one the engine writes a value with, so the text can be handed straight back to the
    // property: floats in the invariant culture the engine parses them with, the rest as the engine reads them.
    private static string FormatDefault(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        string[] texts => string.Join(';', texts),
        Identifier[] identifiers => string.Join(';', identifiers),
        Identifier identifier => identifier.ToString(),
        float number => number.ToString("G", CultureInfo.InvariantCulture),
        int number => number.ToString(CultureInfo.CurrentCulture),
        ushort number => number.ToString(CultureInfo.CurrentCulture),
        Point point => XMLExtensions.PointToString(point),
        Vector2 vector => XMLExtensions.Vector2ToString(vector),
        Vector3 vector => XMLExtensions.Vector3ToString(vector, "G"),
        Vector4 vector => XMLExtensions.Vector4ToString(vector, "G"),
        Rectangle rect => XMLExtensions.RectToString(rect),
        Color color => color.ToStringHex(),
        Enum enumeration => enumeration.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private readonly record struct ResolvedTarget(Type? Type, ContentXElement? Element);

    private static ResolvedTarget Resolve(string identifier, string componentName, int componentIndex)
    {
        ContentXElement? root = TryGetPrefab(identifier, out ItemPrefab prefab) ? prefab.ConfigElement : null;

        if (string.IsNullOrWhiteSpace(componentName)) { return new ResolvedTarget(typeof(Item), root); }
        if (root is null) { return default; }

        int wanted = Math.Max(1, componentIndex);
        int index = 0;

        // The same count the game uses when it resolves an override: the Nth element with this name.
        foreach (ContentXElement element in root.Elements())
        {
            if (!IsComponent(element)) { continue; }
            if (!string.Equals(element.Name.LocalName, componentName, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (++index != wanted) { continue; }

            return ComponentTypeFor(componentName) is { } type ? new ResolvedTarget(type, element) : default;
        }

        return default;
    }

    // The element name either names a component class or it is not a component at all. A capitalized name left over
    // is listed as well: a plugin can add component types of its own, known only to the game's plugin data.
    private static bool IsComponent(ContentXElement element)
    {
        string name = element.Name.LocalName;
        if (string.IsNullOrEmpty(name) || NonComponentElements.Contains(name)) { return false; }

        return ComponentTypeFor(name) is not null || char.IsUpper(name[0]);
    }

    private static Type? ComponentTypeFor(string name) => ComponentTypes.TryGetValue(name, out Type? type) ? type : null;

    // Nothing but the class name is registered: the game looks a component up by exactly the element name, so a
    // shortened spelling would only offer targets it would never create.
    private static FrozenDictionary<string, Type> ComponentTypes => _componentTypes ??= BuildComponentTypes();

    private static FrozenDictionary<string, Type> BuildComponentTypes()
    {
        Dictionary<string, Type> types = new(StringComparer.OrdinalIgnoreCase);

        // The one place the mod asks what the engine's component kinds are, and it asks through the engine's own
        // helper: an override names a component by its class name and there is no list of them to read instead.
        // This is type discovery, not reaching a member the assemblies have hidden.
        foreach (Type type in ReflectionUtils.GetDerivedNonAbstract<ItemComponent>())
        {
            types.TryAdd(type.Name, type);
        }

        return types.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryGetPrefab(string identifier, out ItemPrefab prefab)
    {
        if (ItemPrefabLookup.By(identifier) is { } found)
        {
            prefab = found;
            return true;
        }

        prefab = null!;
        return false;
    }

    private static OverrideTarget ItemItself => new(string.Empty, 1, TextManager.Get("sandboxmenu.pick.itemitself"));

    // Elements the item's own constructor reads itself (a sprite, a physics body, a price, a status effect
    // override, an upgrade), which the component loader is never asked about: taking one for a component would
    // offer a target the game cannot create.
    private static readonly HashSet<string> NonComponentElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "sprite", "brokensprite", "inventoryicon", "decorativesprite", "infectedsprite",
        "damagedinfectedsprite", "containedsprite", "upgradepreviewsprite", "minimapicon",
        "body", "staticbody", "price", "commonness", "levelcommonness", "trigger", "aitarget",
        "attackoverride", "statuseffectoverride", "skillrequirementhint", "deconstruct",
        "deconstructitem", "fabricate", "fabricable", "fabricableitem", "fabricationrecipe",
        "upgrade", "upgrademodule", "upgradeoverride", "swappableitem", "hidelinkedentity",
        "hidelinkedentities", "suitabletreatment", "preferredcontainer",
        "name", "aliases", "tag", "description", "variantof", "linkedto", "cargo", "relation"
    };
}
