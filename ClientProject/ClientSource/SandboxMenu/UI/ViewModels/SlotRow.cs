namespace SandboxMenu.UI.ViewModels;

public sealed class SlotRow(EntryEditorViewModel editor, ItemEntry item, Action browse)
    : RowViewModel(TextManager.Get("sandboxmenu.field.equipslots"))
{
    private readonly ItemEntry _item = item;

    public LocalizedString Summary
    {
        get
        {
            InvSlotType[]? slots = _item.EquipSlots;

            return slots is null || slots.Length == 0
                ? TextManager.Get("sandboxmenu.slots.none")
                : string.Join(", ", SpawnSlots.All.Where(slots.Contains));
        }
    }

    public RelayCommand BrowseCommand { get; } = new(browse);

    internal void Set(InvSlotType slot, bool ticked)
    {
        InvSlotType[]? slots = ticked
            ? [.. _item.EquipSlots ?? [], slot]
            : [.. (_item.EquipSlots ?? []).Where(kept => kept != slot)];

        _item.EquipSlots = slots.Length == 0 ? null : [.. SpawnSlots.All.Where(slots.Contains)];
        Raise(nameof(Summary));
        editor.NotifyEdited();
    }
}
