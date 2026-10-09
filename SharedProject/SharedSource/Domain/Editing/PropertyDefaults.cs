using System.Globalization;

namespace SandboxMenu.Domain.Editing;

internal static class PropertyDefaults
{
    internal static ContentXElement? ElementOf(ItemPrefab? prefab, string componentName, int componentIndex)
    {
        if (prefab?.ConfigElement is not { } root) { return null; }
        if (string.IsNullOrWhiteSpace(componentName)) { return root; }

        int wanted = Math.Max(1, componentIndex);
        int seen = 0;

        foreach (ContentXElement element in root.Elements())
        {
            if (!string.Equals(element.Name.LocalName, componentName, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (++seen == wanted) { return element; }
        }

        return null;
    }

    internal static string DeclaredValue(SerializableProperty property, ContentXElement? element)
        => element?.GetAttribute(property.Name)?.Value is { } declared
            ? declared
            : property.Attributes.OfType<Serialize>().FirstOrDefault() is { } serialize
                ? Format(serialize.DefaultValue)
                : string.Empty;

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
