using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

internal sealed class EntryEditorViewModel : Notifiable
{
    private readonly Action<SpawnEntry> _refreshEntry;
    private readonly Action _refreshEntries;
    private readonly EditorRows _rowBuilder;

    private SpawnEntry? _entry;
    private ItemEntry? _container;
    private bool _rebuildQueued;

    internal EntryEditorViewModel(
        IDialogHost host,
        Action<ItemEntry> giveToCharacter,
        Action<StatusKind, LocalizedString> report,
        Action<SpawnEntry> refreshEntry,
        Action refreshEntries)
    {
        _refreshEntry = refreshEntry;
        _refreshEntries = refreshEntries;
        _rowBuilder = new EditorRows(this, host, giveToCharacter, report);

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

    internal void Refresh()
    {
        if (_entry is { } entry)
        {
            _refreshEntry(entry);
            return;
        }

        _refreshEntries();
    }

    internal void FocusIdentifier()
    {
        if (Rows.OfType<BrowseRowViewModel>().FirstOrDefault() is not { } row) { return; }

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

        _refreshEntries();

        _rowBuilder.Build(_entry, _container);
    }
}
