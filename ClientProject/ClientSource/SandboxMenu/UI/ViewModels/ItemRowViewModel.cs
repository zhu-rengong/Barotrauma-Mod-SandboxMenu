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
        }
    }

    public virtual RichString Title => Display?.Title ?? string.Empty;

    public virtual RichString SubText => Display?.Tags ?? RichString.Rich(string.Empty);

    public virtual Sprite? Icon => Display?.Icon;

    public virtual RichString ToolTip => Display?.ToolTip ?? string.Empty;
}
