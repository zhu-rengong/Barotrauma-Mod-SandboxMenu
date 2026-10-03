using Barotrauma.Items.Components;

namespace SandboxMenu.Domain.Spawning;

// What a spawned item asks of the character it belongs to: which slot it goes into, and where its wifi channel
// comes from.
internal static class CharacterEquipment
{
    internal static void Equip(Character character, Item item, InvSlotType[]? allowedSlots)
    {
        Inventory? inventory = character.Inventory;
        if (inventory is null) { return; }

        List<InvSlotType> slots = [.. allowedSlots is { Length: > 0 } ? allowedSlots : item.AllowedSlots];
        slots.Remove(InvSlotType.Any);
        if (slots.Count == 0) { slots.Add(InvSlotType.Any); }

        inventory.TryPutItem(item, character, slots);
    }

    internal static void InheritChannel(Item item, Character character)
    {
        WifiComponent? targetWifi = item.GetComponent<WifiComponent>();
        if (targetWifi is null) { return; }

        Item? headset = character.Inventory.GetItemInLimbSlot(InvSlotType.Headset);
        WifiComponent? sourceWifi = headset?.GetComponent<WifiComponent>();
        if (sourceWifi is null) { return; }

        targetWifi.Channel = sourceWifi.Channel;
        targetWifi.TeamID = sourceWifi.TeamID;
        targetWifi.AllowCrossTeamCommunication = sourceWifi.AllowCrossTeamCommunication;
    }
}
