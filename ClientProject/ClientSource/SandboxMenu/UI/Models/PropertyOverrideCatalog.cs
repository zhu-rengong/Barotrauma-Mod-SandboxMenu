using System.Collections.Immutable;
using System.Globalization;
using Barotrauma.Items.Components;

namespace SandboxMenu.UI.Models;

internal readonly record struct OverrideTarget(string ComponentName, int ComponentIndex, LocalizedString Label);

internal enum PropertyKind
{
    Float,
    Int,
    Bool,
    Text,
    Vector2,
    Point,
    Color,

    Enum,
    Flags
}

internal readonly record struct NumericRange(float Min, float Max, int Decimals, float Step);

internal sealed record PropertyOption(
    string Name,
    string TypeName,
    string DefaultValue,
    bool Editable,
    bool Saveable,
    PropertyKind Kind,
    NumericRange? Range,
    ImmutableArray<string> Values)
{
    public string Label => $"{Name} ({TypeName}) = {DefaultValue}";
}

internal static class PropertyOverrideCatalog
{
    private static readonly Dictionary<string, ImmutableArray<PropertyOption>> _propertyCache = new(StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<OverrideTarget> Targets(string identifier) =>
        DescribeTargets((identifier ?? string.Empty).Trim());

    internal static IReadOnlyList<PropertyOption> Properties(string identifier, string componentName, int componentIndex)
    {
        ResolvedTarget target = Resolve(identifier, componentName, componentIndex);
        if (target.Type is not { } type) { return []; }

        ImmutableArray<PropertyOption> declared = Declared(type);

        return target.Element is not { } element
            ? declared
            : [.. declared.Select(option => AsDeclaredBy(option, element))];
    }

    internal static PropertyOption? Describe(string identifier, string componentName, int componentIndex, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) { return null; }

        ResolvedTarget target = Resolve(identifier, componentName, componentIndex);
        if (target.Type is not { } type) { return null; }

        foreach (PropertyOption option in Declared(type))
        {
            if (!string.Equals(option.Name, propertyName, StringComparison.OrdinalIgnoreCase)) { continue; }

            return target.Element is { } element ? AsDeclaredBy(option, element) : option;
        }

        return null;
    }

    internal static LocalizedString TargetLabel(string identifier, string componentName, int componentIndex)
    {
        int wanted = Math.Max(1, componentIndex);

        foreach (OverrideTarget target in DescribeTargets((identifier ?? string.Empty).Trim()))
        {
            if (target.ComponentIndex != wanted) { continue; }
            if (!string.Equals(target.ComponentName, componentName ?? string.Empty, StringComparison.OrdinalIgnoreCase)) { continue; }

            return target.Label;
        }

        return string.IsNullOrEmpty(componentName) ? ItemItself.Label : componentName;
    }

    private static ImmutableArray<PropertyOption> Declared(Type type)
    {
        if (_propertyCache.TryGetValue(type.Name, out ImmutableArray<PropertyOption> cached)) { return cached; }

        ImmutableArray<PropertyOption> built = [.. DescribeProperties(type)];
        _propertyCache[type.Name] = built;

        return built;
    }

    private static PropertyOption AsDeclaredBy(PropertyOption option, ContentXElement element)
        => element.GetAttribute(option.Name)?.Value is { } declared ? option with { DefaultValue = declared } : option;

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

            bool editable = property.Attributes.OfType<Editable>().Any();
            bool saveable = serialize.IsSaveable == IsPropertySaveable.Yes;

            options.Add(new PropertyOption(
                property.Name,
                kind,
                PropertyDefaults.Format(serialize.DefaultValue),
                editable,
                saveable,
                KindOf(typeName, property.PropertyType),
                RangeOf(property, typeName),
                ValuesOf(property.PropertyType)));
        }

        options.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return options;
    }

    private static PropertyKind KindOf(string typeName, Type type) => typeName switch
    {
        "float" => PropertyKind.Float,
        "int" => PropertyKind.Int,
        "bool" => PropertyKind.Bool,
        "vector2" => PropertyKind.Vector2,
        "point" => PropertyKind.Point,
        "color" => PropertyKind.Color,
        "Enum" => type.IsDefined(typeof(FlagsAttribute), false) ? PropertyKind.Flags : PropertyKind.Enum,
        _ => PropertyKind.Text
    };

    private static NumericRange? RangeOf(SerializableProperty property, string typeName)
    {
        if (property.Attributes.OfType<Editable>().FirstOrDefault() is not { } editable) { return null; }

        float step = editable.ValueStep > 0f ? editable.ValueStep : 1f;

        return typeName switch
        {
            "float" or "vector2" => new NumericRange(editable.MinValueFloat, editable.MaxValueFloat, Math.Max(1, editable.DecimalCount), step),
            "int" or "point" => new NumericRange(editable.MinValueInt, editable.MaxValueInt, 0, step),
            _ => null
        };
    }

    private static ImmutableArray<string> ValuesOf(Type type)
        => type.IsEnum ? [.. Enum.GetNames(type)] : [];

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

    private static Type? ComponentTypeFor(string name)
    {
        foreach (Type type in ReflectionUtils.GetDerivedNonAbstract<ItemComponent>())
        {
            if (string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase)) { return type; }
        }

        foreach (PluginData pluginData in PluginData.LoadedPluginData)
        {
            foreach (Type type in pluginData.ItemComponents)
            {
                if (string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase)) { return type; }
            }
        }

        return string.Equals(typeof(ItemComponent).Name, name, StringComparison.OrdinalIgnoreCase) ? typeof(ItemComponent) : null;
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
