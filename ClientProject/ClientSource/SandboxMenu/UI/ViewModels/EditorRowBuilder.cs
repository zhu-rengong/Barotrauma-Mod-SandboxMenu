using System.Collections.ObjectModel;
using Barotrauma.Items.Components;

namespace SandboxMenu.UI.ViewModels;

// What the editor puts up for one entry: the spawn section, the amount fields and, for an item, the behaviour and
// property rows. Each row writes back through the entry and tells the editor to refresh the tree summaries.
internal sealed class EditorRowBuilder(EntryEditorViewModel editor, SpawnMenuViewModel menu)
{
    private const float AmountLimit = 100000f;

    private readonly ObservableCollection<IEditorRow> _rows = editor.Rows;

    internal void Build(SpawnEntry? entry, ItemEntry? container)
    {
        if (entry is null)
        {
            _rows.Add(new HintRow(TextManager.Get("sandboxmenu.empty")));
            return;
        }

        _rows.Add(new SectionRow(TextManager.Get("sandboxmenu.section.spawn")));

        switch (entry)
        {
            case ItemEntry item:
                AddTargetRows(item, container);
                AddAmountRows(item);
                AddBehaviourRows(item);
                break;

            case ReferenceEntry reference:
                AddTemplateRow(reference);
                AddAmountRows(reference);
                break;
        }
    }

    private void AddAmountRows(SpawnEntry entry)
    {
        _rows.Add(new RangeRow(
            editor,
            TextManager.Get("sandboxmenu.field.amount"),
            entry.Amount,
            1f,
            AmountLimit,
            value => entry.Amount = value));

        if (entry is ItemEntry item)
        {
            _rows.Add(new RangeRow(
                editor,
                TextManager.Get("sandboxmenu.field.stacks"),
                item.Stacks,
                1f,
                AmountLimit,
                value => item.Stacks = value));
        }

        _rows.Add(new TickRow(
            TextManager.Get("sandboxmenu.field.amountround"),
            entry.AmountRound,
            value => entry.AmountRound = value,
            editor.NotifyEdited));

        if (entry is ItemEntry filling)
        {
            AddTick("sandboxmenu.field.fillinventory", filling.FillInventory, value => filling.FillInventory = value);
        }
    }

    private void AddTargetRows(ItemEntry item, ItemEntry? container)
    {
        ItemPreviewRow preview = ItemPreviewRow.For(item.Identifier);

        _rows.Add(new BrowseRow(
            editor,
            TextManager.Get("sandboxmenu.field.identifier"),
            item.Identifier,
            value =>
            {
                item.Identifier = value;
                preview.Show(value);
            },
            () => menu.Host.ShowItemBrowser(identifier => FrameActions.Post(() =>
            {
                item.Identifier = identifier;
                editor.Rebuild();
                editor.NotifyEdited();
            }), container, identifier => FrameActions.Post(() =>
            {
                item.Identifier = identifier;
                editor.Rebuild();
                editor.NotifyEdited();
                menu.GiveToCharacter(item);
            }))));

        _rows.Add(preview);
    }

    private void AddBehaviourRows(ItemEntry item)
    {
        _rows.Add(new SectionRow(TextManager.Get("sandboxmenu.section.behaviour")));
        AddTick("sandboxmenu.field.equip", item.Equip, value => item.Equip = value);
        SlotRow? slots = null;
        slots = new SlotRow(editor, item, () => BrowseEquipSlots(item, slots));
        _rows.Add(slots);
        AddTick("sandboxmenu.field.install", item.Install, value => item.Install = value);
        AddTick("sandboxmenu.field.inheritchannel", item.InheritChannel, value => item.InheritChannel = value);

        _rows.Add(new IntRow(
            editor,
            TextManager.Get("sandboxmenu.field.slotindex"),
            item.SlotIndex,
            0,
            32,
            value => item.SlotIndex = value));

        _rows.Add(new IntRow(
            editor,
            TextManager.Get("sandboxmenu.field.quality"),
            item.Quality,
            0,
            Quality.MaxQuality,
            value => item.Quality = value));

        _rows.Add(new TextRow(
            editor,
            TextManager.Get("sandboxmenu.field.tags"),
            item.Tags ?? string.Empty,
            value => item.Tags = string.IsNullOrWhiteSpace(value) ? null : value));

        BuildProperties(item);
    }

    private void BuildProperties(ItemEntry item)
    {
        _rows.Add(new SectionRow(TextManager.Get("sandboxmenu.section.properties")));

        int index = 0;
        foreach (PropertyOverride target in item.Properties.ToList())
        {
            index++;

            PropertyRow? row = null;
            row = new PropertyRow(
                editor,
                index,
                target,
                PropertyOverrideCatalog.TargetLabel(item.Identifier, target.ComponentName, target.ComponentIndex),
                PropertyOverrideCatalog.Describe(item.Identifier, target.ComponentName, target.ComponentIndex, target.PropertyName),
                () => BrowseComponents(item, row),
                () => BrowseProperties(item, row),
                () => BrowseEnums(row),
                () => PickColor(row),
                () => FrameActions.Post(() =>
                {
                    item.Properties.Remove(target);
                    editor.Rebuild();
                    editor.NotifyEdited();
                }));

            _rows.Add(row);
        }

        _rows.Add(new ButtonRow(TextManager.Get("sandboxmenu.addproperty"), () => FrameActions.Post(() =>
        {
            item.Properties.Add(new PropertyOverride());
            editor.Rebuild();
            editor.NotifyEdited();
        })));
    }

    private void AddTemplateRow(ReferenceEntry reference)
        => _rows.Add(new TextRow(
            editor,
            TextManager.Get("sandboxmenu.field.template"),
            reference.TemplateName,
            value => reference.TemplateName = value));

    private void AddTick(string labelKey, bool value, Action<bool> apply)
        => _rows.Add(new TickRow(TextManager.Get(labelKey), value, apply, editor.NotifyEdited));

    private void BrowseComponents(ItemEntry item, PropertyRow? row)
    {
        if (row is null) { return; }

        menu.Host.ShowOptions(
            TextManager.Get("sandboxmenu.browse.component"),
            PropertyOverrideCatalog.Targets(item.Identifier)
                .Select(target => new PickerOption(target.Label, () => SetComponent(item, row, target))));
    }

    // The property stays only while the new component declares it the same way: the metadata behind the row's editor is
    // taken again from the target the name would be read from.
    private static void SetComponent(ItemEntry item, PropertyRow row, OverrideTarget target)
        => row.SetComponent(
            target,
            PropertyOverrideCatalog.Describe(item.Identifier, target.ComponentName, target.ComponentIndex, row.PropertyName));

    private void BrowseEnums(PropertyRow? row)
    {
        if (row?.Descriptor is not { } declared || declared.Values.Length == 0) { return; }

        // A flags enum holds several values at once, which the host's own editors draw as a set of tick boxes; a
        // plain one is a single choice.
        if (declared.Kind == PropertyKind.Flags)
        {
            menu.Host.ShowMultiPicker(
                TextManager.Get("sandboxmenu.browse.enumvalue"),
                declared.Values.Select(value => new PickerToggle(
                    value,
                    () => row.HasEnumFlag(value),
                    on => row.SetEnumFlag(value, on))));

            return;
        }

        menu.Host.ShowOptions(
            TextManager.Get("sandboxmenu.browse.enumvalue"),
            declared.Values.Select(value => new PickerOption(value, () => row.SetEnumValue(value))),
            filterable: true);
    }

    private void PickColor(PropertyRow? row)
    {
        if (row is null) { return; }

        menu.Host.ShowColorPicker(row.Swatch, color => row.Swatch = color);
    }

    private void BrowseEquipSlots(ItemEntry item, SlotRow? row)
    {
        if (row is null) { return; }

        menu.Host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.field.equipslots"),
            SpawnSlots.All.Select(slot => new PickerToggle(
                slot.ToString(),
                () => item.EquipSlots?.Contains(slot) == true,
                ticked => row.Set(slot, ticked))));
    }

    private void BrowseProperties(ItemEntry item, PropertyRow? row)
    {
        if (row is null) { return; }

        IReadOnlyList<PropertyOption> options =
            PropertyOverrideCatalog.Properties(item.Identifier, row.ComponentName, row.ComponentIndex);

        if (options.Count == 0)
        {
            menu.Report(TextManager.Get("sandboxmenu.pick.noproperties"));
            return;
        }

        menu.Host.ShowOptions(
            TextManager.Get("sandboxmenu.browse.property"),
            options.Select(option => new PickerOption(option.Label, () => row.SetProperty(option), option.Editable, option.Saveable)),
            filterable: true);
    }
}
