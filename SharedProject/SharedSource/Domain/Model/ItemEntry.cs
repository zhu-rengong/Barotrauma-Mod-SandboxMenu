using System.Xml.Linq;

namespace SandboxMenu.Domain.Model;

internal sealed class ItemEntry : SpawnEntry
{
    public string Identifier { get; set; } = string.Empty;

    public ValueRange? Stacks { get; set; }

    public bool FillInventory { get; set; }

    public int? Quality { get; set; }

    public string? Tags { get; set; }

    public bool Equip { get; set; }

    public InvSlotType[]? EquipSlots { get; set; }

    public int? SlotIndex { get; set; }

    public bool Install { get; set; }

    public bool InheritChannel { get; set; }

    public List<PropertyOverride> Properties { get; } = [];

    public List<SpawnEntry> Inventory { get; } = [];

    public override XElement ToXml()
    {
        XElement element = new("Item", new XAttribute("identifier", Identifier));
        WriteCommon(element);

        XmlValue.WriteRange(element, "stacks", Stacks);
        XmlValue.WriteBool(element, "fillInventory", FillInventory);
        XmlValue.WriteInt(element, "quality", Quality);
        XmlValue.WriteString(element, "tags", Tags);
        XmlValue.WriteBool(element, "equip", Equip);
        XmlValue.WriteString(element, "equipSlots", SpawnSlots.ToXml(EquipSlots));
        XmlValue.WriteInt(element, "slotIndex", SlotIndex);
        XmlValue.WriteBool(element, "install", Install);
        XmlValue.WriteBool(element, "inheritChannel", InheritChannel);

        XElement? properties = null;

        foreach (PropertyOverride property in Properties)
        {
            if (!property.IsNamed) { continue; }

            (properties ??= new XElement("Properties")).Add(property.ToXml());
        }

        if (properties is not null) { element.Add(properties); }

        if (Inventory.Count > 0)
        {
            XElement inventory = new("Inventory");
            foreach (SpawnEntry entry in Inventory) { inventory.Add(entry.ToXml()); }
            element.Add(inventory);
        }

        return element;
    }

    internal static ItemEntry Read(XElement element)
    {
        ItemEntry entry = new()
        {
            Identifier = element.GetAttributeString("identifier", string.Empty),
            FillInventory = element.GetAttributeBool("fillInventory", false),
            Tags = element.Attribute("tags")?.Value,
            Equip = element.GetAttributeBool("equip", false),
            Install = element.GetAttributeBool("install", false),
            InheritChannel = element.GetAttributeBool("inheritChannel", false)
        };
        entry.ReadCommon(element);

        entry.Stacks = XmlValue.ReadRange(element, "stacks");
        entry.Quality = element.Attribute("quality") is null ? null : element.GetAttributeInt("quality", 0);
        entry.SlotIndex = element.Attribute("slotIndex") is null ? null : element.GetAttributeInt("slotIndex", 0);

        entry.EquipSlots = SpawnSlots.FromXml(element.Attribute("equipSlots")?.Value);

        if (element.Element("Properties") is { } properties)
        {
            foreach (XElement property in properties.Elements("Property"))
            {
                PropertyOverride read = PropertyOverride.FromXml(property);
                if (!read.IsNamed) { continue; }

                entry.Properties.Add(read);
            }
        }

        if (element.Element("Inventory") is { } inventory)
        {
            foreach (XElement child in inventory.Elements())
            {
                SpawnEntry? childEntry = FromXml(child);
                if (childEntry is not null) { entry.Inventory.Add(childEntry); }
            }
        }

        return entry;
    }

    protected override SpawnEntry CloneCore()
    {
        ItemEntry clone = new()
        {
            Identifier = Identifier,
            Stacks = Stacks,
            FillInventory = FillInventory,
            Quality = Quality,
            Tags = Tags,
            Equip = Equip,
            EquipSlots = EquipSlots?.ToArray(),
            SlotIndex = SlotIndex,
            Install = Install,
            InheritChannel = InheritChannel
        };

        CopyCommonTo(clone);

        foreach (PropertyOverride property in Properties) { clone.Properties.Add(property.Clone()); }
        foreach (SpawnEntry child in Inventory) { clone.Inventory.Add(child.Clone()); }

        return clone;
    }
}
