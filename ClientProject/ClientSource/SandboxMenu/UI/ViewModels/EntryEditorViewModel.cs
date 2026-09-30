using System.Collections.ObjectModel;
using Barotrauma.Items.Components;

namespace SandboxMenu.UI.ViewModels;

internal sealed class EntryEditorViewModel : Notifiable
{
    private const float AmountLimit = 100000f;

    private readonly SpawnMenuViewModel _menu;

    private SpawnEntry? _entry;
    private ItemEntry? _container;
    private bool _rebuildQueued;

    internal EntryEditorViewModel(SpawnMenuViewModel menu)
    {
        _menu = menu;

        Rebuild();
    }

    public ObservableCollection<IEditorRow> Rows { get; } = [];

    internal void Show(SpawnEntry entry, ItemEntry? container = null)
    {
        _entry = entry;
        _container = container;

        QueueRebuild();
    }

    internal void Clear()
    {
        _entry = null;
        _container = null;

        QueueRebuild();
    }

    internal void NotifyEdited() => _menu.RefreshSummaries();

    // The identifier is the first row the editor puts up for an item: asking it for the keyboard again is what the
    // "type the identifier" command does (no rebuild, so nothing being typed is disturbed).
    internal void FocusIdentifier()
    {
        if (Rows.OfType<BrowseRow>().FirstOrDefault() is not { } row) { return; }

        row.TakeFocus = false;
        row.TakeFocus = true;
    }

    internal void ReleaseContent()
    {
        foreach (IEditorRow row in Rows)
        {
            if (row is ItemRowViewModel item) { item.Release(); }
        }
    }

    private void QueueRebuild()
    {
        if (_rebuildQueued) { return; }

        _rebuildQueued = true;
        MenuActions.Enqueue(() =>
        {
            if (!_rebuildQueued) { return; }

            Rebuild();
        });
    }

    internal void Rebuild()
    {
        _rebuildQueued = false;
        Rows.Clear();

        _menu.RefreshSummaries();

        if (_entry is null)
        {
            Rows.Add(new HintRow(TextManager.Get("sandboxmenu.empty")));
            return;
        }

        Rows.Add(new SectionRow(TextManager.Get("sandboxmenu.section.spawn")));

        switch (_entry)
        {
            case ItemEntry item:
                AddTargetRows(item);
                AddAmountRows(item);
                AddBehaviourRows(item);
                break;

            case RefEntry reference:
                AddTemplateRow(reference);
                AddAmountRows(reference);
                break;
        }
    }

    private void AddAmountRows(SpawnEntry entry)
    {
        Rows.Add(new RangeRow(
            this,
            TextManager.Get("sandboxmenu.field.amount"),
            entry.Amount,
            1f,
            AmountLimit,
            value => entry.Amount = value));

        if (entry is ItemEntry item)
        {
            Rows.Add(new RangeRow(
                this,
                TextManager.Get("sandboxmenu.field.stacks"),
                item.Stacks,
                1f,
                AmountLimit,
                value => item.Stacks = value));
        }

        Rows.Add(new TickRow(
            TextManager.Get("sandboxmenu.field.amountround"),
            entry.AmountRound,
            value => entry.AmountRound = value,
            NotifyEdited));

        if (entry is ItemEntry filling)
        {
            AddTick("sandboxmenu.field.fillinventory", filling.FillInventory, value => filling.FillInventory = value);
        }
    }

    private void AddTargetRows(ItemEntry item)
    {
        ItemPreviewRow preview = ItemPreviewRow.For(item.Identifier);

        Rows.Add(new BrowseRow(
            this,
            TextManager.Get("sandboxmenu.field.identifier"),
            item.Identifier,
            value =>
            {
                item.Identifier = value;
                preview.Show(value);
            },
            () => _menu.Host.ShowItemBrowser(identifier => MenuActions.Enqueue(() =>
            {
                item.Identifier = identifier;
                Rebuild();
                NotifyEdited();
            }), _container)));

        Rows.Add(preview);
    }

    private void AddBehaviourRows(ItemEntry item)
    {
        Rows.Add(new SectionRow(TextManager.Get("sandboxmenu.section.behaviour")));
        AddTick("sandboxmenu.field.equip", item.Equip, value => item.Equip = value);
        SlotRow? slots = null;
        slots = new SlotRow(this, item, () => BrowseEquipSlots(item, slots));
        Rows.Add(slots);
        AddTick("sandboxmenu.field.install", item.Install, value => item.Install = value);
        AddTick("sandboxmenu.field.inheritchannel", item.InheritChannel, value => item.InheritChannel = value);

        Rows.Add(new IntRow(
            this,
            TextManager.Get("sandboxmenu.field.slotindex"),
            item.SlotIndex,
            0,
            32,
            value => item.SlotIndex = value));

        Rows.Add(new IntRow(
            this,
            TextManager.Get("sandboxmenu.field.quality"),
            item.Quality,
            0,
            Quality.MaxQuality,
            value => item.Quality = value));

        Rows.Add(new TextRow(
            this,
            TextManager.Get("sandboxmenu.field.tags"),
            item.Tags ?? string.Empty,
            value => item.Tags = string.IsNullOrWhiteSpace(value) ? null : value));

        BuildProperties(item);
    }

    private void BuildProperties(ItemEntry item)
    {
        Rows.Add(new SectionRow(TextManager.Get("sandboxmenu.section.properties")));

        int index = 0;
        foreach (PropertyOverride target in item.Properties.ToList())
        {
            index++;

            PropertyRow? row = null;
            row = new PropertyRow(
                this,
                index,
                target,
                () => BrowseComponents(item, row),
                () => BrowseProperties(item, row),
                () => MenuActions.Enqueue(() =>
                {
                    item.Properties.Remove(target);
                    Rebuild();
                    NotifyEdited();
                }));

            Rows.Add(row);
        }

        Rows.Add(new ButtonRow(TextManager.Get("sandboxmenu.addproperty"), () => MenuActions.Enqueue(() =>
        {
            item.Properties.Add(new PropertyOverride());
            Rebuild();
            NotifyEdited();
        })));
    }

    private void AddTemplateRow(RefEntry reference)
        => Rows.Add(new TextRow(
            this,
            TextManager.Get("sandboxmenu.field.template"),
            reference.TemplateName,
            value => reference.TemplateName = value));

    private void AddTick(string labelKey, bool value, Action<bool> apply)
        => Rows.Add(new TickRow(TextManager.Get(labelKey), value, apply, NotifyEdited));

    private void BrowseComponents(ItemEntry item, PropertyRow? row)
    {
        if (row is null) { return; }

        _menu.Host.ShowOptions(
            TextManager.Get("sandboxmenu.browse.component"),
            PropertyOverrideCatalog.Targets(item.Identifier)
                .Select(target => new PickerOption(target.Label, () => row.SetComponent(target))));
    }

    private void BrowseEquipSlots(ItemEntry item, SlotRow? row)
    {
        if (row is null) { return; }

        _menu.Host.ShowMultiPicker(
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
            _menu.Report(TextManager.Get("sandboxmenu.pick.noproperties"));
            return;
        }

        _menu.Host.ShowOptions(
            TextManager.Get("sandboxmenu.browse.property"),
            options.Select(option => new PickerOption(option.Label, () => row.SetProperty(option), option.Editable, option.Saveable)),
            filterable: true);
    }
}
