namespace SandboxMenu.UI.ViewModels;

internal sealed class ItemPreviewRowViewModel : ItemRowViewModel
{
    private string _fallbackTitle = string.Empty;
    private LocalizedString _fallbackSubText = LocalizedString.EmptyString;

    private ItemPreviewRowViewModel() => Show(string.Empty);

    public override RichString Title => Display is null ? _fallbackTitle : base.Title;

    public override RichString SubText => Display is null ? RichString.Rich(_fallbackSubText) : base.SubText;

    internal static ItemPreviewRowViewModel For(string identifier)
    {
        ItemPreviewRowViewModel row = new();
        row.Show(identifier);
        return row;
    }

    internal void Show(string identifier)
    {
        bool blank = string.IsNullOrWhiteSpace(identifier);

        Display = ItemDisplay.For(identifier);

        _fallbackTitle = blank ? string.Empty : identifier;

        _fallbackSubText = TextManager.Get(blank ? "sandboxmenu.preview.none" : "sandboxmenu.preview.missing");

        Raise(nameof(Title));
        Raise(nameof(SubText));
    }
}
