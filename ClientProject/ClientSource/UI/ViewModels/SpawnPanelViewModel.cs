using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.ViewModels;

internal sealed class SpawnPanelViewModel : Notifiable, IItemDropTarget, IListBackground
{
    private readonly IDialogHost _host;
    private readonly TreeRows _tree;
    private readonly TreeDragDrop _treeEditor;
    private readonly PresetFiles _presets;
    private readonly SpawnActions _requests;
    private readonly Action<StatusKind, LocalizedString> _report;

    private TreeEntryViewModel? _selectedEntry;
    private WeakReference<Character>? _selectedRef;
    private CharacterDisplay? _display;
    private float _iconToolTipHealth = float.NaN;

    private Character? _selected
    {
        get
        {
            if (_selectedRef is not { } reference) { return null; }
            if (!reference.TryGetTarget(out Character? character) || character is null) { return null; }

            return character.Removed || !Character.CharacterList.Contains(character) || !ClientRequestPolicy.CanTarget(character)
                ? null
                : character;
        }
    }

    internal SpawnPanelViewModel(IDialogHost host, Action<StatusKind, LocalizedString> report)
    {
        _host = host;
        _report = report;
        _tree = new TreeRows(Set, Select, row => ShowContextMenu(row));
        Editor = new EntryEditorViewModel(host, GiveToCharacter, report, RefreshEntry, RefreshEntries);
        _treeEditor = new TreeDragDrop(Set, _tree, () => SelectedEntry, Select, ClearSelection);
        _presets = new PresetFiles(report, host, Set, ReloadSet);
        _requests = new SpawnActions(report, host, Set, () => _selected);

        OpenBrowserCommand = new RelayCommand(() => _host.ShowCharacterBrowser(Select));
        SelectControlledCommand = new RelayCommand(() => Select(Character.Controlled));
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
        DuplicateSelectedCommand = new RelayCommand(_treeEditor.DuplicateSelected);
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

    public RelayCommand OpenBrowserCommand { get; }

    public RelayCommand SelectControlledCommand { get; }

    public CharacterDisplay? Display => _display;

    public bool ShowIcon => _display is not null;

    public RichString IconToolTip => _display?.ToolTip ?? RichString.Rich(string.Empty);

    public RichString Title => _display is null
        ? RichString.Rich(TextManager.Get("sandboxmenu.itemspawn.none"))
        : RichString.Rich(TooltipText.Bright(_display.Name));

    internal TreeRows Tree => _tree;

    internal void Tick()
    {
        if (_selected is not { } current) { SelectCore(Character.Controlled, force: _selectedRef is not null); }
        else if (_display is null) { SelectCore(current); }
    }

    internal void RefreshIconToolTip()
    {
        if (_display is not { } display) { return; }

        float health = display.Health;

        if (health == _iconToolTipHealth) { return; }

        _iconToolTipHealth = health;

        Raise(nameof(IconToolTip));
    }

    internal void Select(Character? character)
        => SelectCore(character, force: _selectedRef is not null && _selected is null);

    private void SelectCore(Character? character, bool force = false)
    {
        if (!ClientRequestPolicy.CanTarget(character)) { return; }
        if (!force && ReferenceEquals(_selected, character) && (character is null || _display is not null)) { return; }

        _selectedRef = character is null ? null : new WeakReference<Character>(character);
        _display = character is null ? null : new CharacterDisplay(character);
        _iconToolTipHealth = float.NaN;

        using (NotificationBatch.Begin())
        {
            Raise(nameof(Display));
            Raise(nameof(ShowIcon));
            Raise(nameof(IconToolTip));
            Raise(nameof(Title));
        }
    }

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

    public RelayCommand DuplicateSelectedCommand { get; }

    public RelayCommand AddItemCommand { get; }

    public RelayCommand AddChildCommand { get; }

    public LocalizedString GiveShortcut => KeyBinds.Give.ToString();

    internal void Report(StatusKind kind, LocalizedString message) => _report(kind, message);

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

    internal void RefreshEntries() => _tree.Refresh();

    internal void RefreshEntry(SpawnEntry entry) => _tree.Refresh(entry);

    internal void Rebuild() => FrameActions.Post(Editor.Rebuild);

    internal void ReleaseContent()
    {
        _tree.Release();
        Editor.ReleaseContent();

        _display = null;
        _selectedRef = null;
        _iconToolTipHealth = float.NaN;

        using (NotificationBatch.Begin())
        {
            Raise(nameof(Display));
            Raise(nameof(ShowIcon));
            Raise(nameof(IconToolTip));
            Raise(nameof(Title));
        }
    }

    public void ShowContextMenu(Vector2 position) => ShowContextMenu(null, position);

    internal void ShowContextMenu(TreeEntryViewModel? row, Vector2? position = null)
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

            actions.Add(new MenuCommand("sandboxmenu.duplicate", () => _treeEditor.Duplicate(row),
                TextManager.Get("sandboxmenu.shortcut.duplicate")));

            actions.Add(new MenuCommand("sandboxmenu.focusidentifier", FocusIdentifier, TextManager.Get("sandboxmenu.shortcut.enter")));

            actions.Add(new MenuCommand("sandboxmenu.give", () => _requests.Give([row.Entry])));
            actions.Add(new MenuCommand("sandboxmenu.itemspawn.atcursor", () => _requests.AtCursor([row.Entry])));

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

        Report(StatusKind.Success, TextManager.Get("sandboxmenu.status.cleared"));
    }
}
