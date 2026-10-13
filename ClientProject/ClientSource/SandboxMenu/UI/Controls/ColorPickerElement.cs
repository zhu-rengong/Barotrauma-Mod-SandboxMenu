namespace SandboxMenu.UI.Controls;

[Element("ColorPicker")]
public sealed class ColorPickerElement : ViewElement, IDisposable
{
    private readonly GUIFrame _holder;

    private Action<Color>? _changed;
    private GUIColorPicker? _picker;
    private Color _color = Color.White;
    private Color _frameColor = UiMetrics.TextDim;
    private float _frameThickness = Theme.LineThickness * 3f;

    public ColorPickerElement(ElementContext context)
        : base(new GUIFrame(context.Rect(context.Parent, 1f, 1f), style: null) { CanBeFocused = false })
    {
        _holder = (GUIFrame)Control;

        _holder.RectTransform.SizeChanged += EnsurePicker;
        _holder.RectTransform.ScaleChanged += EnsurePicker;

        EnsurePicker();
    }

    [ElementProperty]
    public new Color Color
    {
        set
        {
            _color = value;
            Push();
        }
    }

    [ElementProperty]
    public Action<Color>? Changed { set => _changed = value; }

    [ElementProperty]
    public Color FrameColor { set => _frameColor = value; }

    [ElementProperty]
    public float FrameThickness { set => _frameThickness = Math.Max(0f, value); }

    public void Dispose()
    {
        _holder.RectTransform.SizeChanged -= EnsurePicker;
        _holder.RectTransform.ScaleChanged -= EnsurePicker;

        if (_picker is not { } picker) { return; }

        picker.OnColorSelected = null;
        picker.Dispose();
        _picker = null;
    }

    private void EnsurePicker()
    {
        if (_picker is not null || _holder.Rect.Height <= 0) { return; }

        Rectangle area = _holder.Rect;
        int thickness = UiMetrics.DipInt(_frameThickness);

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

                Theme.Outline(spriteBatch, frame, _frameColor, thickness);
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
