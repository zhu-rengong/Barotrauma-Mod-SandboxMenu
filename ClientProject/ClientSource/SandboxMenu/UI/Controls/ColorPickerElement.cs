namespace SandboxMenu.UI.Controls;

// The host's colour picker as a markup element: the view says where it sits, the element keeps it in step with the
// colour it is bound to and hands every colour picked out of it back through Changed.
[Element("ColorPicker")]
public sealed class ColorPickerElement : ViewElement, IDisposable
{
    private readonly GUIFrame _holder;

    private Action<Color>? _changed;
    private GUIColorPicker? _picker;
    private Color _color = Color.White;

    public ColorPickerElement(ElementContext context)
        : base(new GUIFrame(context.Rect(context.Parent, 1f, 1f), style: null) { CanBeFocused = false })
    {
        _holder = (GUIFrame)Control;

        // The host picker measures its gradient once, on its first update, and keeps that size for good: it is built a
        // frame late, once the layout has handed this element the size it will keep.
        context.View.EveryFrame(EnsurePicker);
    }

    [ElementProperty]
    public Color Color
    {
        set
        {
            _color = value;
            Push();
        }
    }

    [ElementProperty]
    public Action<Color>? Changed { set => _changed = value; }

    public void Dispose()
    {
        if (_picker is not { } picker) { return; }

        picker.OnColorSelected = null;
        picker.Dispose();
        _picker = null;
    }

    private void EnsurePicker()
    {
        if (_picker is not null || _holder.Rect.Height <= 0) { return; }

        Rectangle area = _holder.Rect;
        int thickness = UiMetrics.DipInt(Theme.LineThickness * 3f);

        // The picker is inset by the frame's thickness so that the frame can sit outside the gradient: a frame drawn
        // on the picker's own rectangle would land on top of the colours.
        Vector2 fill = new(
            Math.Max(1, area.Width - thickness * 2) / (float)Math.Max(1, area.Width),
            Math.Max(1, area.Height - thickness * 2) / (float)Math.Max(1, area.Height));

        GUIColorPicker picker = new(new RectTransform(fill, _holder.RectTransform, Anchor.Center), null);
        _picker = picker;

        picker.OnColorSelected = (_, color) =>
        {
            _changed?.Invoke(color);
            return true;
        };

        new GUICustomComponent(
            new RectTransform(Vector2.One, picker.RectTransform),
            (spriteBatch, _) =>
            {
                Rectangle frame = picker.Rect;
                frame.Inflate(thickness, thickness);

                Theme.Outline(spriteBatch, frame, UiMetrics.TextDim, thickness);
            })
        {
            CanBeFocused = false
        };

        Push();
    }

    private void Push()
    {
        if (_picker is not { } picker) { return; }

        Vector3 hsv = ToolBox.RGBToHSV(_color);
        float hue = float.IsNaN(hsv.X) ? 0f : hsv.X;
        bool hueMoved = Math.Abs(hue - picker.SelectedHue) > 0.001f;

        picker.SelectedHue = hue;
        picker.SelectedSaturation = hsv.Y;
        picker.SelectedValue = hsv.Z;
        picker.CurrentColor = _color;

        // Regenerating the gradient is the expensive part of the picker, so it only follows a hue that moved; before
        // the picker's first update there is nothing to regenerate, and it builds its gradient with this hue.
        if (hueMoved) { picker.RefreshHue(); }
    }
}
