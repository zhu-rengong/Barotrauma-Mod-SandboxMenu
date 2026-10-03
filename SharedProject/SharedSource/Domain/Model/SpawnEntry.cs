using System.Collections.Frozen;
using System.Xml.Linq;

namespace SandboxMenu.Domain.Model;

internal abstract class SpawnEntry
{
    private static FrozenDictionary<string, Func<XElement, SpawnEntry>>? _readers;

    static SpawnEntry() => ModLifetime.Unloading += () => _readers = null;

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
        XmlValue.WriteRange(element, "amount", Amount);
        XmlValue.WriteBool(element, "amountRound", AmountRound);
    }

    protected void ReadCommon(XElement element)
    {
        Amount = XmlValue.ReadRange(element, "amount");
        AmountRound = element.GetAttributeBool("amountRound", false);
    }
}
