using System.Collections.ObjectModel;
using Barotrauma.Items.Components;

namespace SandboxMenu.UI.ViewModels;

internal sealed class EditorRows(
    EntryEditorViewModel editor,
    IDialogHost host,
    Action<ItemEntry> giveToCharacter,
    Action<StatusKind, LocalizedString> report)
{
    private const float AmountLimit = 100000f;

    private readonly ObservableCollection<IEditorRow> _rows = editor.Rows;

    internal void Build(SpawnEntry? entry, ItemEntry? container)
    {
        if (entry is null)
        {
            _rows.Add(new HintRowViewModel(TextManager.Get("sandboxmenu.empty")));
            return;
        }

        _rows.Add(new SectionRowViewModel(TextManager.Get("sandboxmenu.section.spawn")));

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
        _rows.Add(new RangeRowViewModel(
            editor,
            TextManager.Get("sandboxmenu.field.amount"),
            entry.Amount,
            1f,
            AmountLimit,
            value => entry.Amount = value));

        if (entry is ItemEntry item)
        {
            _rows.Add(new RangeRowViewModel(
                editor,
                TextManager.Get("sandboxmenu.field.stacks"),
                item.Stacks,
                1f,
                AmountLimit,
                value => item.Stacks = value));
        }

        _rows.Add(new TickRowViewModel(
            TextManager.Get("sandboxmenu.field.amountround"),
            entry.AmountRound,
            value => entry.AmountRound = value,
            editor.Refresh));

        if (entry is ItemEntry filling)
        {
            AddTick("sandboxmenu.field.fillinventory", filling.FillInventory, value => filling.FillInventory = value);
        }
    }

    private void AddTargetRows(ItemEntry item, ItemEntry? container)
    {
        ItemPreviewRowViewModel preview = ItemPreviewRowViewModel.For(item.Identifier);

        void Take(string identifier)
        {
            item.Identifier = identifier;
            editor.Rebuild();
            editor.Refresh();
        }

        _rows.Add(new BrowseRowViewModel(
            editor,
            TextManager.Get("sandboxmenu.field.identifier"),
            item.Identifier,
            value =>
            {
                item.Identifier = value;
                preview.Show(value);
            },
            () => host.ShowItemBrowser(
                identifier => FrameActions.Post(() => Take(identifier)),
                container,
                identifier => FrameActions.Post(() =>
                {
                    Take(identifier);
                    giveToCharacter(item);
                }))));

        _rows.Add(preview);
    }

    private void AddBehaviourRows(ItemEntry item)
    {
        _rows.Add(new SectionRowViewModel(TextManager.Get("sandboxmenu.section.behaviour")));
        AddTick("sandboxmenu.field.equip", item.Equip, value => item.Equip = value);
        SlotRowViewModel? slots = null;
        slots = new SlotRowViewModel(editor, item, () => BrowseEquipSlots(item, slots));
        _rows.Add(slots);
        AddTick("sandboxmenu.field.install", item.Install, value => item.Install = value);
        AddTick("sandboxmenu.field.inheritchannel", item.InheritChannel, value => item.InheritChannel = value);

        _rows.Add(new IntRowViewModel(
            editor,
            TextManager.Get("sandboxmenu.field.slotindex"),
            item.SlotIndex,
            0,
            32,
            value => item.SlotIndex = value));

        _rows.Add(new IntRowViewModel(
            editor,
            TextManager.Get("sandboxmenu.field.quality"),
            item.Quality,
            0,
            Quality.MaxQuality,
            value => item.Quality = value));

        _rows.Add(new TextRowViewModel(
            editor,
            TextManager.Get("sandboxmenu.field.tags"),
            item.Tags ?? string.Empty,
            value => item.Tags = string.IsNullOrWhiteSpace(value) ? null : value));

        BuildProperties(item);
    }

    private void BuildProperties(ItemEntry item)
    {
        _rows.Add(new SectionRowViewModel(TextManager.Get("sandboxmenu.section.properties")));

        int index = 0;
        foreach (PropertyOverride target in item.Properties.ToList())
        {
            index++;

            PropertyRowViewModel? row = null;
            row = new PropertyRowViewModel(
                editor,
                index,
                target,
                PropertyCatalog.TargetLabel(item.Identifier, target.ComponentName, target.ComponentIndex),
                PropertyCatalog.Describe(item.Identifier, target.ComponentName, target.ComponentIndex, target.PropertyName),
                () => BrowseComponents(item, row),
                () => BrowseProperties(item, row),
                () => BrowseEnums(row),
                () => PickColor(row),
                () => FrameActions.Post(() =>
                {
                    item.Properties.Remove(target);
                    editor.Rebuild();
                    editor.Refresh();
                }));

            _rows.Add(row);
        }

        _rows.Add(new ButtonRowViewModel(TextManager.Get("sandboxmenu.addproperty"), () => FrameActions.Post(() =>
        {
            item.Properties.Add(new PropertyOverride());
            editor.Rebuild();
            editor.Refresh();
        })));
    }

    private void AddTemplateRow(ReferenceEntry reference)
        => _rows.Add(new TextRowViewModel(
            editor,
            TextManager.Get("sandboxmenu.field.template"),
            reference.TemplateName,
            value => reference.TemplateName = value));

    private void AddTick(string labelKey, bool value, Action<bool> apply)
        => _rows.Add(new TickRowViewModel(TextManager.Get(labelKey), value, apply, editor.Refresh));

    private void BrowseComponents(ItemEntry item, PropertyRowViewModel? row)
    {
        if (row is null) { return; }

        host.ShowOptions(
            TextManager.Get("sandboxmenu.browse.component"),
            PropertyCatalog.Targets(item.Identifier)
                .Select(target => new PickerOption(target.Label, () => SetComponent(item, row, target))));
    }

    private static void SetComponent(ItemEntry item, PropertyRowViewModel row, OverrideTarget target)
        => row.SetComponent(
            target,
            PropertyCatalog.Describe(item.Identifier, target.ComponentName, target.ComponentIndex, row.PropertyName));

    private void BrowseEnums(PropertyRowViewModel? row)
    {
        if (row?.Descriptor is not { } declared || declared.Values.Length == 0) { return; }

        if (declared.Kind == PropertyKind.Flags)
        {
            host.ShowMultiPicker(
                TextManager.Get("sandboxmenu.browse.enumvalue"),
                declared.Values.Select(value => new PickerToggle(
                    value,
                    () => row.HasEnumFlag(value),
                    on => row.SetEnumFlag(value, on))));

            return;
        }

        host.ShowOptions(
            TextManager.Get("sandboxmenu.browse.enumvalue"),
            declared.Values.Select(value => new PickerOption(value, () => row.SetEnumValue(value))),
            filterable: true);
    }

    private void PickColor(PropertyRowViewModel? row)
    {
        if (row is null) { return; }

        host.ShowColorPicker(row.Swatch, color => row.Swatch = color);
    }

    private void BrowseEquipSlots(ItemEntry item, SlotRowViewModel? row)
    {
        if (row is null) { return; }

        host.ShowMultiPicker(
            TextManager.Get("sandboxmenu.field.equipslots"),
            SpawnSlots.All.Select(slot => new PickerToggle(
                slot.ToString(),
                () => item.EquipSlots?.Contains(slot) == true,
                ticked => row.Set(slot, ticked))));
    }

    private void BrowseProperties(ItemEntry item, PropertyRowViewModel? row)
    {
        if (row is null) { return; }

        IReadOnlyList<PropertyOption> options =
            PropertyCatalog.Properties(item.Identifier, row.ComponentName, row.ComponentIndex);

        if (options.Count == 0)
        {
            report(StatusKind.Warning, TextManager.Get("sandboxmenu.pick.noproperties"));
            return;
        }

        host.ShowOptions(
            TextManager.Get("sandboxmenu.browse.property"),
            options.Select(option => new PickerOption(option.Label, () => row.SetProperty(option), option.Editable, option.Saveable)),
            filterable: true);
    }
}
