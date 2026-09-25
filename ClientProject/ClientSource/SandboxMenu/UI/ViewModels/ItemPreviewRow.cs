namespace SandboxMenu.UI.ViewModels;

public sealed class ItemPreviewRow : ItemRowViewModel
{
    private string _fallbackTitle = string.Empty;
    private LocalizedString _fallbackSubText = LocalizedString.EmptyString;

    private ItemPreviewRow() => Show(string.Empty);

    public override RichString Title => Display is null ? _fallbackTitle : base.Title;

    public override RichString SubText => Display is null ? RichString.Rich(_fallbackSubText) : base.SubText;

    internal static ItemPreviewRow For(string identifier)
    {
        var row = new ItemPreviewRow();
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
