namespace UiFramework.Controls;

[Element("Image")]
internal sealed class ImageElement : ViewElement
{
    private readonly GUIImage _image;

    public ImageElement(ElementContext context)
        : this(Create(context))
    {
    }

    private ImageElement(GUIImage image) : base(image)
    {
        _image = image;

        _image.OverrideState = GUIComponent.ComponentState.None;
        _image.HoverCursor = CursorState.Default;
    }

    [ElementProperty]
    public RichString? ToolTip { set => _image.ToolTip = value ?? string.Empty; }

    private static GUIImage Create(ElementContext context)
    {
        RectTransform rect = context.Rect(context.Parent, 1f, 1f);

        if (context.Style(context.Skin)?.GetDefaultSprite() is { } sprite)
        {
            return new GUIImage(rect, sprite, null, GUIImage.ScalingMode.ScaleToFitSmallestExtent);
        }

        return new GUIImage(
            rect,
            context.Skin is { Length: > 0 } skin ? skin : "GUIFrame",
            GUIImage.ScalingMode.None);
    }
}
