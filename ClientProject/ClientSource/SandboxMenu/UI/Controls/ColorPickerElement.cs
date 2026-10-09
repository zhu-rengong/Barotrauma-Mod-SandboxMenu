namespace SandboxMenu.UI.Controls;

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

        bool carriesHue = hsv.X >= 0f && hsv.Y > 0f && hsv.Z > 0f;
        float hue = carriesHue ? hsv.X : picker.SelectedHue;
        float saturation = hsv.Z > 0f ? hsv.Y : picker.SelectedSaturation;

        bool hueMoved = Math.Abs(hue - picker.SelectedHue) > 0.001f;

        picker.SelectedHue = hue;
        picker.SelectedSaturation = saturation;
        picker.SelectedValue = hsv.Z;
        picker.CurrentColor = _color;

        if (hueMoved) { picker.RefreshHue(); }
    }
}
