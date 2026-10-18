using System.Collections.Frozen;
using System.Globalization;

namespace SandboxMenu.Domain.Editing;

internal static class PropertyText
{
    private const int CacheLimit = 512;

    private static readonly FrozenSet<Type> NetworkTypes = new[]
    {
        typeof(string), typeof(string[]), typeof(Identifier), typeof(float), typeof(int), typeof(bool),
        typeof(Color), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Point), typeof(Rectangle)
    }.ToFrozenSet();

    private static readonly Dictionary<(ItemPrefab Prefab, string Component, int Index), ContentXElement?> Elements = [];

    internal static bool Travels(object? value)
        => value is not null && (value.GetType().IsEnum || NetworkTypes.Contains(value.GetType()));

    internal static ContentXElement? ElementOf(ItemPrefab? prefab, string componentName, int componentIndex)
    {
        if (prefab is null) { return null; }

        string component = componentName ?? string.Empty;
        (ItemPrefab, string, int) key = (prefab, component, componentIndex);

        if (Elements.TryGetValue(key, out ContentXElement? cached)) { return cached; }

        ContentXElement? resolved = ResolveElement(prefab, component, componentIndex);

        if (Elements.Count >= CacheLimit) { Elements.Clear(); }

        Elements[key] = resolved;

        return resolved;
    }

    private static ContentXElement? ResolveElement(ItemPrefab prefab, string componentName, int componentIndex)
    {
        if (prefab.ConfigElement is not { } root) { return null; }
        if (string.IsNullOrWhiteSpace(componentName)) { return root; }

        return ComponentOrdinal.Find(root.Elements(), componentName, componentIndex, static element => element.Name.LocalName);
    }

    internal static string DeclaredValue(SerializableProperty property, ContentXElement? element)
    {
        if (element?.GetAttribute(property.Name)?.Value is { } declared) { return declared; }

        foreach (Attribute attribute in property.Attributes)
        {
            if (attribute is Serialize serialize) { return Format(serialize.DefaultValue); }
        }

        return string.Empty;
    }

    internal static string Format(object? value) => value switch
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
}
