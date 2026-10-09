using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.ViewModels;

internal sealed class SpawnPanelViewModel : Notifiable, IItemDropTarget, IListBackground
{
    private readonly IDialogHost _host;
    private readonly SpawnSetTree _tree;
    private readonly EntryTreeEditor _treeEditor;
    private readonly PresetFiles _presets;
    private readonly SpawnRequests _requests;

    private TreeEntryViewModel? _selectedEntry;
    private LocalizedString _status = LocalizedString.EmptyString;
    private bool _treeRefreshQueued;

    internal SpawnPanelViewModel(IDialogHost host)
    {
        _host = host;
        _tree = new SpawnSetTree(this, Set);
        Editor = new EntryEditorViewModel(this);
        _treeEditor = new EntryTreeEditor(this);
        _presets = new PresetFiles(this, Set);
        _requests = new SpawnRequests(this, Set);

        SavePresetCommand = new RelayCommand(_presets.Save);
        LoadPresetCommand = new RelayCommand(_presets.Load);
        ReloadPresetCommand = new RelayCommand(_presets.Reload);
        OpenPresetCommand = new RelayCommand(_presets.Open);
        ClearSetCommand = new RelayCommand(ClearSet);
        SpawnIntoInventoryCommand = new RelayCommand(_requests.Give);
        SpawnAtCursorCommand = new RelayCommand(_requests.AtCursor);

        StepUpCommand = new RelayCommand(() => _treeEditor.StepSelection(-1));
        StepDownCommand = new RelayCommand(() => _treeEditor.StepSelection(1));
        MoveUpCommand = new RelayCommand(() => _treeEditor.MoveSelection(-1));
        MoveDownCommand = new RelayCommand(() => _treeEditor.MoveSelection(1));
        FocusIdentifierCommand = new RelayCommand(FocusIdentifier);
        DeleteSelectedCommand = new RelayCommand(_treeEditor.DeleteSelected);
        AddItemCommand = new RelayCommand(() => _treeEditor.AddItem(asChild: false));
        AddChildCommand = new RelayCommand(() => _treeEditor.AddItem(asChild: true));

        _tree.Rebuild();
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

    internal SpawnSetTree Tree => _tree;

    internal void RefreshHints()
    {
        Editor.RefreshHints();
        _tree.RefreshHints();
    }

    public ObservableCollection<TreeEntryViewModel> Entries => _tree.Rows;

    public RelayCommand SavePresetCommand { get; }

    public RelayCommand LoadPresetCommand { get; }

    public RelayCommand ReloadPresetCommand { get; }

    public RelayCommand OpenPresetCommand { get; }

    public RelayCommand ClearSetCommand { get; }

    public RelayCommand SpawnIntoInventoryCommand { get; }

    internal void GiveToCharacter(ItemEntry entry) => _requests.Give([entry]);

    public RelayCommand SpawnAtCursorCommand { get; }

    public RelayCommand StepUpCommand { get; }

    public RelayCommand StepDownCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public RelayCommand FocusIdentifierCommand { get; }

    public RelayCommand DeleteSelectedCommand { get; }

    public RelayCommand AddItemCommand { get; }

    public RelayCommand AddChildCommand { get; }

    public LocalizedString GiveShortcut => Plugin.GiveKey.ToString();

    public LocalizedString Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    internal void Report(LocalizedString message) => Status = message;

    internal void RefreshShortcuts() => Raise(nameof(GiveShortcut));

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
        FrameActions.Post(() =>
        {
            _tree.Rebuild();

            if (Entries.Count > 0) { Select(Entries[0]); }
            else { Editor.Clear(); }
        });
    }

    internal void Select(TreeEntryViewModel row)
    {
        foreach (TreeEntryViewModel entry in Entries)
        {
            entry.IsSelected = ReferenceEquals(entry, row);
        }

        SelectedEntry = row;
        FrameActions.Post(() => Editor.Show(row.Entry, row.Container));
    }

    internal void ClearSelection()
    {
        SelectedEntry = null;
        Editor.Clear();
    }

    internal void RefreshSummaries() => _tree.RefreshSummaries();

    internal void NotifyEdited()
    {
        if (_treeRefreshQueued) { return; }

        _treeRefreshQueued = true;
        SpawnEntry? selected = SelectedEntry?.Entry;
        FrameActions.Post(() =>
        {
            _treeRefreshQueued = false;
            _tree.Rebuild();

            if (selected is not null && _tree.Find(selected) is { } row)
            {
                foreach (TreeEntryViewModel entry in Entries)
                {
                    entry.IsSelected = ReferenceEquals(entry, row);
                }

                SelectedEntry = row;
            }
        });
    }

    internal void ContentChanged() => FrameActions.Post(Editor.Rebuild);

    internal void ReleaseContent()
    {
        _tree.Release();
        Editor.ReleaseContent();
    }

    public void ShowBackgroundMenu(Vector2 position) => OpenMenu(null, position);

    internal void OpenMenu(TreeEntryViewModel? row, Vector2? position = null)
    {
        if (row is not null) { Select(row); }

        List<MenuCommand> actions = [];

        if (row is null)
        {
            actions.Add(new MenuCommand("sandboxmenu.additem", () => _treeEditor.Add(new ItemEntry(), null), TextManager.Get("sandboxmenu.shortcut.add")));
            actions.Add(new MenuCommand("sandboxmenu.addref", () => _treeEditor.Add(new ReferenceEntry(), null)));
        }
        else
        {
            actions.Add(new MenuCommand("sandboxmenu.additem", () => _treeEditor.Add(new ItemEntry(), row), TextManager.Get("sandboxmenu.shortcut.add")));
            actions.Add(new MenuCommand("sandboxmenu.addref", () => _treeEditor.Add(new ReferenceEntry(), row)));

            if (row.Entry is ItemEntry)
            {
                actions.Add(new MenuCommand("sandboxmenu.addchild", () => _treeEditor.AddChild(row), TextManager.Get("sandboxmenu.shortcut.addchild")));
            }

            actions.Add(new MenuCommand("sandboxmenu.duplicate", () => _treeEditor.Duplicate(row)));

            actions.Add(new MenuCommand("sandboxmenu.focusidentifier", FocusIdentifier, TextManager.Get("sandboxmenu.shortcut.enter")));

            actions.Add(new MenuCommand("sandboxmenu.give", () => _requests.Give([row.Entry])));
            actions.Add(new MenuCommand("sandboxmenu.spawnatcursor", () => _requests.AtCursor([row.Entry])));

            actions.Add(new MenuCommand("sandboxmenu.delete", () => _treeEditor.Delete(row), TextManager.Get("sandboxmenu.shortcut.delete")));

            actions.Add(new MenuCommand("sandboxmenu.previous", () => _treeEditor.Step(row, -1), TextManager.Get("sandboxmenu.shortcut.up")));
            actions.Add(new MenuCommand("sandboxmenu.next", () => _treeEditor.Step(row, 1), TextManager.Get("sandboxmenu.shortcut.down")));

            actions.Add(new MenuCommand("sandboxmenu.moveup", () => _treeEditor.Move(row, -1), TextManager.Get("sandboxmenu.shortcut.moveup")));
            actions.Add(new MenuCommand("sandboxmenu.movedown", () => _treeEditor.Move(row, 1), TextManager.Get("sandboxmenu.shortcut.movedown")));
        }

        _host.ShowContextMenu(actions, position);
    }

    internal void FocusIdentifier() => Editor.FocusIdentifier();

    public bool CanDrag(object item) => item is TreeEntryViewModel;

    public bool CanNest(object target) => target is TreeEntryViewModel { Entry: ItemEntry };

    public void Drop(object source, object? target, DropMode mode) => _treeEditor.Drop(source, target, mode);

    internal void ApplySpawnResult(SpawnStatus status, int queued, int problems) => _requests.ApplyResult(status, queued, problems);

    private void ClearSet()
    {
        Set.Entries.Clear();
        SelectedEntry = null;

        FrameActions.Post(() =>
        {
            _tree.Rebuild();
            Editor.Clear();
        });

        Status = TextManager.Get("sandboxmenu.status.cleared");
    }
}
