using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

internal sealed class EntryEditorViewModel : Notifiable
{
    private readonly SpawnMenuViewModel _menu;
    private readonly EditorRowBuilder _rowBuilder;

    private SpawnEntry? _entry;
    private ItemEntry? _container;
    private bool _rebuildQueued;

    internal EntryEditorViewModel(SpawnMenuViewModel menu)
    {
        _menu = menu;
        _rowBuilder = new EditorRowBuilder(this, menu);

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

    internal void RefreshHints()
    {
        foreach (IEditorRow row in Rows)
        {
            if (row is ItemRowViewModel item) { item.RefreshHints(); }
        }
    }

    private void QueueRebuild()
    {
        if (_rebuildQueued) { return; }

        _rebuildQueued = true;
        FrameActions.Post(() =>
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

        _rowBuilder.Build(_entry, _container);
    }
}
