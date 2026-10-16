namespace SandboxMenu.UI.ViewModels;

internal abstract class ItemRowViewModel : Notifiable, IEditorRow
{
    private ItemDisplay? _display;

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

    internal void RefreshHints()
    {
        Raise(nameof(ToolTip));
        Raise(nameof(TileToolTip));
    }

    public virtual RichString Title => Display?.Title ?? string.Empty;

    public virtual RichString SubText => Display?.Tags ?? RichString.Rich(string.Empty);

    public virtual Sprite? Icon => Display?.Icon;

    public virtual RichString ToolTip => Display?.ToolTip ?? string.Empty;

    public virtual RichString TileToolTip => Display?.TileToolTip ?? string.Empty;
}
