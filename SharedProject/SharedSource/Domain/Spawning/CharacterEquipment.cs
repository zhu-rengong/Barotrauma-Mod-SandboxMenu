using Barotrauma.Items.Components;

namespace SandboxMenu.Domain.Spawning;

internal static class CharacterEquipment
{
    internal static void Equip(Character character, Item item, InvSlotType[]? allowedSlots)
    {
        Inventory? inventory = character.Inventory;
        if (inventory is null) { return; }

        List<InvSlotType> slots = [];

        foreach (InvSlotType slot in allowedSlots is { Length: > 0 } ? allowedSlots : item.AllowedSlots)
        {
            if (slot != InvSlotType.Any) { slots.Add(slot); }
        }

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
