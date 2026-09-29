using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.ViewModels;

internal sealed class SpawnMenuViewModel : Notifiable, IDropTarget, IListBackground
{
    private readonly IDialogHost _host;

    private TreeEntryViewModel? _selectedEntry;
    private LocalizedString _status = LocalizedString.EmptyString;
    private bool _treeRefreshQueued;

    internal SpawnMenuViewModel(IDialogHost host)
    {
        _host = host;
        Editor = new EntryEditorViewModel(this);

        SavePresetCommand = new RelayCommand(SavePreset);
        LoadPresetCommand = new RelayCommand(LoadPreset);
        ReloadPresetCommand = new RelayCommand(ReloadPreset);
        OpenPresetCommand = new RelayCommand(OpenPresetFile);
        ClearSetCommand = new RelayCommand(ClearSet);
        SpawnIntoInventoryCommand = new RelayCommand(SpawnIntoInventory);
        SpawnAtCursorCommand = new RelayCommand(SpawnAtCursor);

        RefreshTree();
    }

    public SpawnSet Set { get; } = new();

    public string PresetName
    {
        get => Set.Name;
        set
        {
            string name = value ?? string.Empty;
            if (name == Set.Name) { return; }

            Set.Name = name;
            Raise(nameof(PresetName));
        }
    }

    internal IDialogHost Host => _host;

    public EntryEditorViewModel Editor { get; }

    public ObservableCollection<TreeEntryViewModel> Entries { get; } = [];

    public RelayCommand SavePresetCommand { get; }

    public RelayCommand LoadPresetCommand { get; }

    public RelayCommand ReloadPresetCommand { get; }

    public RelayCommand OpenPresetCommand { get; }

    public RelayCommand ClearSetCommand { get; }

    public RelayCommand SpawnIntoInventoryCommand { get; }

    public RelayCommand SpawnAtCursorCommand { get; }

    public LocalizedString Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    internal void Report(LocalizedString message) => Status = message;

    public TreeEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        private set => Set(ref _selectedEntry, value);
    }

    internal void ReloadSet(SpawnSet source)
    {
        PresetName = source.Name;
        Set.Entries.Clear();
        Set.Entries.AddRange(source.Entries);

        SelectedEntry = null;
        MenuActions.Enqueue(() =>
        {
            RebuildTree();
            Editor.Clear();
        });
    }

    internal void Select(TreeEntryViewModel row)
    {
        foreach (TreeEntryViewModel entry in Entries)
        {
            entry.IsSelected = ReferenceEquals(entry, row);
        }

        SelectedEntry = row;
        MenuActions.Enqueue(() => Editor.Show(row.Entry));
    }

    internal void RefreshSummaries()
    {
        foreach (TreeEntryViewModel entry in Entries) { entry.Refresh(); }
    }

    internal void NotifyEdited()
    {
        if (_treeRefreshQueued) { return; }

        _treeRefreshQueued = true;
        SpawnEntry? selected = SelectedEntry?.Entry;
        MenuActions.Enqueue(() =>
        {
            _treeRefreshQueued = false;
            RebuildTree();

            TreeEntryViewModel? row = selected is null ? null : Entries.FirstOrDefault(e => ReferenceEquals(e.Entry, selected));
            if (row is not null)
            {
                foreach (TreeEntryViewModel entry in Entries)
                {
                    entry.IsSelected = ReferenceEquals(entry, row);
                }

                SelectedEntry = row;
            }
        });
    }

    internal void ContentChanged() => MenuActions.Enqueue(Editor.Rebuild);

    public void ShowBackgroundMenu(Vector2 position) => OpenMenu(null);

    internal void OpenMenu(TreeEntryViewModel? row)
    {
        if (row is not null) { Select(row); }

        List<MenuAction> actions = [];

        if (row is null)
        {
            actions.Add(new MenuAction("sandboxmenu.additem", () => AddEntry(new ItemEntry(), null)));
            actions.Add(new MenuAction("sandboxmenu.addref", () => AddEntry(new RefEntry(), null)));
        }
        else
        {
            actions.Add(new MenuAction("sandboxmenu.additem", () => AddEntry(new ItemEntry(), row)));
            actions.Add(new MenuAction("sandboxmenu.addref", () => AddEntry(new RefEntry(), row)));

            if (row.Entry is ItemEntry)
            {
                actions.Add(new MenuAction("sandboxmenu.addchild", () => AddChild(row)));
            }

            actions.Add(new MenuAction("sandboxmenu.duplicate", () => Duplicate(row)));

            actions.Add(new MenuAction("sandboxmenu.give", () => SpawnIntoInventory([row.Entry])));
            actions.Add(new MenuAction("sandboxmenu.spawnatcursor", () => SpawnAtCursor([row.Entry])));

            actions.Add(new MenuAction("sandboxmenu.delete", () => Delete(row)));
        }

        _host.ShowContextMenu(actions, PlayerInput.MousePosition);
    }

    private void AddEntry(SpawnEntry entry, TreeEntryViewModel? after)
    {
        List<SpawnEntry> owner = after?.Owner ?? Set.Entries;
        int index = after is null ? owner.Count : Math.Clamp(owner.IndexOf(after.Entry) + 1, 0, owner.Count);

        owner.Insert(index, entry);
        SelectNewEntry(entry, owner);
    }

    private void AddChild(TreeEntryViewModel row)
    {
        if (row.Entry is not ItemEntry item) { return; }

        ItemEntry child = new();
        item.Inventory.Add(child);
        SelectNewEntry(child, item.Inventory);
    }

    private void Duplicate(TreeEntryViewModel row)
    {
        SpawnEntry clone = row.Entry.Clone();
        int index = row.Owner.IndexOf(row.Entry);
        row.Owner.Insert(index < 0 ? row.Owner.Count : index + 1, clone);

        SelectNewEntry(clone, row.Owner);
    }

    private void Delete(TreeEntryViewModel row)
    {
        int index = row.Owner.IndexOf(row.Entry);
        if (index < 0) { return; }

        row.Owner.RemoveAt(index);
        SpawnEntry? next = row.Owner.Count == 0 ? null : row.Owner[Math.Min(index, row.Owner.Count - 1)];

        MenuActions.Enqueue(() =>
        {
            RebuildTree();

            TreeEntryViewModel? selected = next is null
                ? null
                : Entries.FirstOrDefault(e => ReferenceEquals(e.Entry, next));

            if (selected is not null) { Select(selected); }
            else { SelectedEntry = null; Editor.Clear(); }
        });
    }

    private void SelectNewEntry(SpawnEntry entry, List<SpawnEntry> owner)
    {
        MenuActions.Enqueue(() =>
        {
            RebuildTree();

            TreeEntryViewModel? row = Entries.FirstOrDefault(e => ReferenceEquals(e.Entry, entry) && ReferenceEquals(e.Owner, owner))
                                      ?? Entries.FirstOrDefault(e => ReferenceEquals(e.Entry, entry));

            if (row is not null) { Select(row); }
        });
    }

    private void RefreshTree() => RebuildTree();

    private void RebuildTree()
    {
        Entries.Clear();
        Flatten(Set.Entries, 0);
    }

    private void Flatten(List<SpawnEntry> entries, int depth)
    {
        foreach (SpawnEntry entry in entries)
        {
            Entries.Add(new TreeEntryViewModel(this, entry, entries, depth));

            if (entry is ItemEntry { Inventory.Count: > 0 } item)
            {
                Flatten(item.Inventory, depth + 1);
            }
        }
    }

    public bool CanDrag(object item) => item is TreeEntryViewModel;

    public bool CanNest(object target) => target is TreeEntryViewModel { Entry: ItemEntry };

    public void Drop(object source, object? target, DropMode mode)
    {
        if (source is not TreeEntryViewModel dragged) { return; }

        TreeEntryViewModel? onto = target as TreeEntryViewModel;
        if (onto is not null && SpawnTree.Contains(dragged.Entry, onto.Entry))
        {
            DropIntoDescendant(dragged, onto, mode);
            return;
        }

        List<SpawnEntry> owner;
        int index;

        if (onto is null)
        {
            owner = Set.Entries;
            index = owner.Count;
        }
        else if (mode == DropMode.Nest && onto.Entry is ItemEntry container)
        {
            owner = container.Inventory;
            index = owner.Count;
        }
        else
        {
            owner = onto.Owner;
            index = owner.IndexOf(onto.Entry) + (mode == DropMode.After ? 1 : 0);
        }

        bool sameList = ReferenceEquals(owner, dragged.Owner);
        int from = dragged.Owner.IndexOf(dragged.Entry);
        if (from < 0) { return; }

        dragged.Owner.RemoveAt(from);
        if (sameList && from < index) { index--; }

        owner.Insert(Math.Clamp(index, 0, owner.Count), dragged.Entry);
        SelectNewEntry(dragged.Entry, owner);
    }

    private void DropIntoDescendant(TreeEntryViewModel dragged, TreeEntryViewModel onto, DropMode mode)
    {
        if (dragged.Entry is not ItemEntry moved || ReferenceEquals(dragged, onto)) { return; }

        if (mode == DropMode.Nest)
        {
            if (onto.Entry is ItemEntry container && SpawnTree.ReparentIntoDescendant(dragged.Owner, moved, container))
            {
                SelectNewEntry(moved, container.Inventory);
            }

            return;
        }

        bool liftedTarget = ReferenceEquals(onto.Owner, moved.Inventory);
        if (!liftedTarget && onto.Owner.IndexOf(onto.Entry) < 0) { return; }

        if (!SpawnTree.LiftBranch(dragged.Owner, moved, onto.Entry)) { return; }

        List<SpawnEntry> owner = liftedTarget ? dragged.Owner : onto.Owner;
        int index = owner.IndexOf(onto.Entry);
        if (index < 0) { return; }

        owner.Insert(Math.Clamp(index + (mode == DropMode.After ? 1 : 0), 0, owner.Count), moved);
        SelectNewEntry(moved, owner);
    }

    private void SavePreset()
    {
        if (string.IsNullOrWhiteSpace(PresetName))
        {
            Status = TextManager.Get("sandboxmenu.status.nopresetname");
            return;
        }

        if (TemplateStore.ReachesTemplate(Set, PresetName))
        {
            Status = TextManager.Get("sandboxmenu.status.templatecycle");
            return;
        }

        Status = TemplateStore.Save(Set)
            ? TextManager.Get("sandboxmenu.status.presetsaved")
            : TextManager.GetWithVariable("sandboxmenu.status.presetsavefailed", "[name]", PresetName);
    }

    private void ClearSet()
    {
        Set.Entries.Clear();
        SelectedEntry = null;

        MenuActions.Enqueue(() =>
        {
            RebuildTree();
            Editor.Clear();
        });

        Status = TextManager.Get("sandboxmenu.status.cleared");
    }

    private void LoadPreset()
    {
        IReadOnlyList<string> presets = TemplateStore.ListPresets();
        if (presets.Count == 0)
        {
            Status = TextManager.Get("sandboxmenu.status.nopresets");
            return;
        }

        _host.ShowOptions(
            TextManager.Get("sandboxmenu.preset.load"),
            presets.Select(name => new PickerOption(name, () => LoadPreset(name))));
    }

    private void LoadPreset(string name)
    {
        if (TryApplyPreset(name))
        {
            Status = TextManager.GetWithVariable("sandboxmenu.status.presetloaded", "[name]", name);
        }
    }

    private void ReloadPreset()
    {
        string name = PresetName;
        if (string.IsNullOrWhiteSpace(name))
        {
            Status = TextManager.Get("sandboxmenu.status.nopresetname");
            return;
        }

        if (TryApplyPreset(name))
        {
            Status = TextManager.GetWithVariable("sandboxmenu.status.presetreloaded", "[name]", name);
        }
    }

    private void OpenPresetFile()
    {
        string name = PresetName;
        if (string.IsNullOrWhiteSpace(name))
        {
            Status = TextManager.Get("sandboxmenu.status.nopresetname");
            return;
        }

        if (!TemplateStore.Exists(name))
        {
            Status = TextManager.GetWithVariable("sandboxmenu.status.presetnotfound", "[name]", name);
            return;
        }

        string path = TemplateStore.PathOf(name);

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Status = TextManager.GetWithVariable("sandboxmenu.status.presetopened", "[name]", name);
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to open the preset file '{path}'", e);
            Status = TextManager.GetWithVariable("sandboxmenu.status.presetopenfailed", "[name]", name);
        }
    }

    private bool TryApplyPreset(string name)
    {
        if (TemplateStore.TryLoad(name, out SpawnSet? set) && set is not null)
        {
            set.Name = name;
            ReloadSet(set);
            return true;
        }

        Status = TextManager.GetWithVariable("sandboxmenu.status.presetloadfailed", "[name]", name);
        return false;
    }

    private void SpawnIntoInventory() => SpawnIntoInventory(Set.Entries);

    private void SpawnAtCursor() => SpawnAtCursor(Set.Entries);

    private void SpawnIntoInventory(IReadOnlyList<SpawnEntry> entries)
    {
        if (Character.Controlled is not { } character)
        {
            Status = TextManager.Get("sandboxmenu.status.nocharacter");
            return;
        }

        if (entries.Count == 0)
        {
            Status = TextManager.Get("sandboxmenu.status.empty");
            return;
        }

        if (ClientSpawnDispatcher.IsMultiplayerClient)
        {
            Status = ReportSend(ClientSpawnDispatcher.TrySendIntoInventory(entries));
            return;
        }

        ItemSpawnService.SpawnIntoInventory(entries, character, ResolveTemplate);
        Status = TextManager.Get("sandboxmenu.status.spawnqueued");
    }

    private void SpawnAtCursor(IReadOnlyList<SpawnEntry> entries)
    {
        if (Character.Controlled is null)
        {
            Status = TextManager.Get("sandboxmenu.status.nocharacter");
            return;
        }

        if (entries.Count == 0)
        {
            Status = TextManager.Get("sandboxmenu.status.empty");
            return;
        }

        _host.PickWorldPosition(worldPosition =>
        {
            if (ClientSpawnDispatcher.IsMultiplayerClient)
            {
                Status = ReportSend(ClientSpawnDispatcher.TrySendToWorld(entries, worldPosition));
                return;
            }

            ItemSpawnService.SpawnAtWorld(entries, worldPosition, Character.Controlled, ResolveTemplate);
            Status = TextManager.Get("sandboxmenu.status.spawnqueued");
        });
    }

    internal void ApplySpawnResult(SpawnStatus status, int queued, int problems)
    {
        LocalizedString word = status switch
        {
            SpawnStatus.Ok => TextManager.GetWithVariable("sandboxmenu.status.serverok", "[count]", queued.ToString()),
            SpawnStatus.Denied => TextManager.Get("sandboxmenu.status.serverdenied"),
            SpawnStatus.BadPayload => TextManager.Get("sandboxmenu.status.serverbadpayload"),
            SpawnStatus.NoCharacter => TextManager.Get("sandboxmenu.status.servernocharacter"),
            SpawnStatus.NoSpawner => TextManager.Get("sandboxmenu.status.servernospawner"),
            SpawnStatus.Throttled => TextManager.Get("sandboxmenu.status.serverthrottled"),
            SpawnStatus.TooLarge => TextManager.Get("sandboxmenu.status.servertoolarge"),
            _ => TextManager.Get("sandboxmenu.status.serverfailed")
        };

        Status = problems == 0
            ? word
            : word + " " + TextManager.GetWithVariable("sandboxmenu.status.serverproblems", "[count]", problems.ToString());
    }

    private static LocalizedString ReportSend(SpawnRefusal? refusal) => refusal switch
    {
        SpawnRefusal.TooLarge => TextManager.Get("sandboxmenu.status.spawntoolarge"),
        SpawnRefusal.NotSent => TextManager.Get("sandboxmenu.status.sendfailed"),
        _ => TextManager.Get("sandboxmenu.status.senttoserver")
    };

    private static SpawnSet? ResolveTemplate(string name)
        => TemplateStore.TryLoad(name, out SpawnSet? set) ? set : null;
}
