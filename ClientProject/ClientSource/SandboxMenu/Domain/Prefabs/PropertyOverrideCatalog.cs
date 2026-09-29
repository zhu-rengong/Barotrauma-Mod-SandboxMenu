using System.Collections.Frozen;
using System.Collections.Immutable;
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
    private static readonly Dictionary<Type, ImmutableArray<PropertyOption>> _propertyCache = [];

    private static FrozenDictionary<string, Type>? _componentTypes;

    static PropertyOverrideCatalog() => StaticState.Register(Clear);

    internal static void Clear()
    {
        _propertyCache.Clear();
        _componentTypes = null;
    }

    internal static IReadOnlyList<OverrideTarget> Targets(string identifier) =>
        DescribeTargets((identifier ?? string.Empty).Trim());

    internal static IReadOnlyList<PropertyOption> Properties(string identifier, string componentName, int componentIndex)
    {
        ResolvedTarget target = Resolve(identifier, componentName, componentIndex);
        if (target.Type is not { } type) { return []; }

        if (!_propertyCache.TryGetValue(type, out ImmutableArray<PropertyOption> declared))
        {
            declared = [.. DescribeProperties(type)];
            _propertyCache[type] = declared;
        }

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
        object probe = RuntimeHelpers.GetUninitializedObject(type);
        List<PropertyOption> options = [];

        foreach (SerializableProperty property in SerializableProperty.GetProperties(probe).Values)
        {
            if (property.Attributes.OfType<Serialize>().FirstOrDefault() is not { } serialize) { continue; }
            if (SerializableProperty.GetSupportedTypeName(property.PropertyType) is not { } typeName) { continue; }

            string kind = string.Equals(typeName, "Enum", StringComparison.Ordinal) ? property.PropertyType.Name : typeName;

            options.Add(new PropertyOption(property.Name, kind, FormatDefault(serialize.DefaultValue)));
        }

        options.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return options;
    }

    private static string FormatDefault(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        string[] texts => string.Join(';', texts),
        Identifier[] identifiers => string.Join(';', identifiers),
        Identifier identifier => identifier.ToString(),
        float number => number.ToString("G", CultureInfo.InvariantCulture),
        int number => number.ToString(CultureInfo.InvariantCulture),
        ushort number => number.ToString(CultureInfo.InvariantCulture),
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

        foreach (ContentXElement element in root.Elements())
        {
            if (!IsComponent(element)) { continue; }
            if (!string.Equals(element.Name.LocalName, componentName, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (++index != wanted) { continue; }

            return ComponentTypeFor(componentName) is { } type ? new ResolvedTarget(type, element) : default;
        }

        return default;
    }

    private static bool IsComponent(ContentXElement element)
    {
        string name = element.Name.LocalName;
        if (string.IsNullOrEmpty(name) || _nonComponentElements.Contains(name)) { return false; }

        return ComponentTypeFor(name) is not null;
    }

    private static Type? ComponentTypeFor(string name) => ComponentTypes.TryGetValue(name, out Type? type) ? type : null;

    private static FrozenDictionary<string, Type> ComponentTypes => _componentTypes ??= BuildComponentTypes();

    private static FrozenDictionary<string, Type> BuildComponentTypes()
    {
        Dictionary<string, Type> types = new(StringComparer.OrdinalIgnoreCase);

        foreach (Type type in ReflectionUtils.GetDerivedNonAbstract<ItemComponent>())
        {
            types.TryAdd(type.Name, type);
        }

        foreach (PluginData pluginData in PluginData.LoadedPluginData)
        {
            foreach (Type type in pluginData.ItemComponents)
            {
                types.TryAdd(type.Name, type);
            }
        }

        types.TryAdd(typeof(ItemComponent).Name, typeof(ItemComponent));

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

    private static readonly HashSet<string> _nonComponentElements = new(StringComparer.OrdinalIgnoreCase)
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
