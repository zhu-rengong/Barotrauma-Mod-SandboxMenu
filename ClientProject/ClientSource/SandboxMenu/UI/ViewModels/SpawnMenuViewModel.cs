using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.ViewModels;

public sealed class SpawnMenuViewModel : Notifiable, IDropTarget, IListBackground
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

    // A localized string rather than text: the line stays on screen until the next action, and switching the
    // language is not one.
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
        MenuActions.Enqueue(() => Editor.Show(row.Entry, row.Owner));
    }

    internal void RefreshSummaries()
    {
        foreach (TreeEntryViewModel entry in Entries) { entry.Refresh(); }
    }

    internal void NotifyEdited()
    {
        // Typing in a field edits the model on every keystroke; one refresh per frame is plenty.
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

    // Built again because a row shows a name and an icon read off a prefab: only what could be read about the
    // entry changed, not the entry itself, and the set holds identifiers and values rather than prefabs.
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

            // The same two commands as the buttons at the bottom of the window, only for this one entry: the one
            // the player pointed at is the one the menu selected on opening.
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

        var child = new ItemEntry();
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

        // A row whose owner no longer holds its entry belongs to a shape the tree has moved on from: its entry
        // stands somewhere else already, and inserting it again would put one entry in two lists at once.
        bool sameList = ReferenceEquals(owner, dragged.Owner);
        int from = dragged.Owner.IndexOf(dragged.Entry);
        if (from < 0) { return; }

        dragged.Owner.RemoveAt(from);
        if (sameList && from < index) { index--; }

        owner.Insert(Math.Clamp(index, 0, owner.Count), dragged.Entry);
        SelectNewEntry(dragged.Entry, owner);
    }

    // An entry dropped onto one of its own descendants travels alone: the entries it holds take its place, so
    // nothing else in the tree changes depth or position — bringing the branch along would re-nest everything
    // under the target.
    private void DropIntoDescendant(TreeEntryViewModel dragged, TreeEntryViewModel onto, DropMode mode)
    {
        if (dragged.Entry is not ItemEntry moved || ReferenceEquals(dragged, onto)) { return; }

        if (mode == DropMode.Nest)
        {
            if (onto.Entry is ItemEntry container && SpawnTree.ReparentIntoDescendant(dragged.Owner, moved, container))
            {
                // Rebuilt like after any other move: a row keeps the list and depth it was built with, and every
                // later drop reads them.
                SelectNewEntry(moved, container.Inventory);
            }

            return;
        }

        // A target held by the dragged entry's own list is the branch that gets lifted, and the lift moves it into
        // the dragged entry's slot: its row's Owner is the one list it will no longer be in. Checked before the
        // lift because undoing one is not a thing — an index nobody can find would leave the entry in no list.
        bool liftedTarget = ReferenceEquals(onto.Owner, moved.Inventory);
        if (!liftedTarget && onto.Owner.IndexOf(onto.Entry) < 0) { return; }

        if (!SpawnTree.LiftBranch(dragged.Owner, moved, onto.Entry)) { return; }

        List<SpawnEntry> owner = liftedTarget ? dragged.Owner : onto.Owner;
        int index = owner.IndexOf(onto.Entry);
        if (index < 0) { return; }

        // Read after the lift, which already took the dragged entry out: this index is the final one.
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

        // A preset reaching back to its own name through the presets it refers to would spawn for ever: refused
        // here, where the player can fix the references, rather than only at spawn time.
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
            // UseShellExecute hands the file to whatever the system registers for it — xdg-open, open, or the
            // default program — instead of trying to run it as an executable.
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
            // The name asked for is the one on the file and the one every button acts on: an older preset may
            // carry a different name inside it, which would send the next reload or save to the wrong file.
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
            // Whoever is asking, the items end up in the inventory of the character the server knows this client
            // controls, so there is nothing to say about which character or which inventory.
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

    // What the server made of a request, taken by the shell on the frame after the answer arrived. The word comes
    // from the server, so the player is told what actually happened rather than what was asked for — and told to
    // look at the log when the server had to leave something out (problems).
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
