using System.Collections.Frozen;
using System.Globalization;
using System.Xml.Linq;

namespace SandboxMenu.Domain.Model;

internal abstract class SpawnEntry
{
    private static FrozenDictionary<string, Func<XElement, SpawnEntry>>? _readers;

    private static FrozenDictionary<string, Func<XElement, SpawnEntry>> Readers => _readers ??= new Dictionary<string, Func<XElement, SpawnEntry>>(StringComparer.Ordinal)
    {
        ["Item"] = static element => ItemEntry.Read(element),
        ["Ref"] = static element => ReferenceEntry.Read(element)
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public ValueRange? Amount { get; set; }

    public bool AmountRound { get; set; }

    public abstract XElement ToXml();

    public SpawnEntry Clone() => CloneCore();

    public static SpawnEntry? FromXml(XElement element)
        => Readers.TryGetValue(element.Name.LocalName, out Func<XElement, SpawnEntry>? read) ? read(element) : null;

    protected abstract SpawnEntry CloneCore();

    protected void CopyCommonTo(SpawnEntry clone)
    {
        clone.Amount = Amount;
        clone.AmountRound = AmountRound;
    }

    protected void WriteCommon(XElement element)
    {
        WriteRange(element, "amount", Amount);

        if (AmountRound) { element.SetAttributeValue("amountRound", "true"); }
    }

    protected void ReadCommon(XElement element)
    {
        Amount = ReadRange(element, "amount");
        AmountRound = element.GetAttributeBool("amountRound", false);
    }

    protected static void WriteRange(XElement element, string prefix, ValueRange? range)
    {
        if (range is not { } value) { return; }

        element.SetAttributeValue(prefix + "Min", value.Min.ToString(CultureInfo.InvariantCulture));

        if (value.IsRange) { element.SetAttributeValue(prefix + "Max", value.Max.ToString(CultureInfo.InvariantCulture)); }
    }

    protected static ValueRange? ReadRange(XElement element, string prefix)
    {
        if (element.Attribute(prefix + "Min") is null) { return null; }

        float min = element.GetAttributeFloat(prefix + "Min", 0f);
        float max = element.GetAttributeFloat(prefix + "Max", min);

        return new ValueRange(min, max);
    }
}
