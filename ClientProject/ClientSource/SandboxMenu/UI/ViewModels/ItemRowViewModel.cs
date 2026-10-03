namespace SandboxMenu.UI.ViewModels;

internal abstract class ItemRowViewModel : Notifiable, IEditorRow
{
    private ItemDisplay? _display;

    // A display holds the prefab it was made for; rows let go of them when the content packages change.
    internal void Release() => Display = null;

    public ItemDisplay? Display
    {
        get => _display;
        protected set
        {
            if (!Set(ref _display, value)) { return; }

            Raise(nameof(Title));
            Raise(nameof(SubText));
            Raise(nameof(Icon));
            Raise(nameof(ToolTip));
            Raise(nameof(TileToolTip));
        }
    }

    // A hint is built from state the game changes without telling anyone — who is being played, above all — and a
    // bound control only takes its value again when the row says it changed.
    internal void RefreshHints()
    {
        Raise(nameof(ToolTip));
        Raise(nameof(TileToolTip));
    }

    public virtual RichString Title => Display?.Title ?? string.Empty;

    public virtual RichString SubText => Display?.Tags ?? RichString.Rich(string.Empty);

    public virtual Sprite? Icon => Display?.Icon;

    public virtual RichString ToolTip => Display?.ToolTip ?? string.Empty;

    // Tiles show the icon alone, so they take the hint that spells out what a row shows: name with the identifier
    // and the tags, then the notes of the item hint.
    public virtual RichString TileToolTip => Display?.TileToolTip ?? string.Empty;
}
