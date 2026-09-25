using System.Globalization;
using System.Xml.Linq;

namespace SandboxMenu.Infrastructure;

internal static class XmlValue
{
    internal static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);

    internal static void WriteRange(XElement element, string prefix, ValueRange? range)
    {
        if (range is not { } r) { return; }

        element.SetAttributeValue(prefix + "Min", Text(r.Min));
        if (r.IsRange) { element.SetAttributeValue(prefix + "Max", Text(r.Max)); }
    }

    internal static ValueRange? ReadRange(XElement element, string prefix)
    {
        if (element.Attribute(prefix + "Min") is null) { return null; }

        float min = element.GetAttributeFloat(prefix + "Min", 0f);
        float max = element.GetAttributeFloat(prefix + "Max", min);
        return new ValueRange(min, max);
    }

    internal static void WriteBool(XElement element, string name, bool value)
    {
        if (value) { element.SetAttributeValue(name, "true"); }
    }

    internal static void WriteInt(XElement element, string name, int? value)
    {
        if (value.HasValue) { element.SetAttributeValue(name, Text(value.Value)); }
    }

    internal static void WriteFloat(XElement element, string name, float value)
        => element.SetAttributeValue(name, Text(value));

    internal static void WriteString(XElement element, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value)) { element.SetAttributeValue(name, value); }
    }
}
