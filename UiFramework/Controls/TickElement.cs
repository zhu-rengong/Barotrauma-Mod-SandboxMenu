namespace UiFramework.Controls;

[Element("Tick")]
internal sealed class TickElement : ViewElement, IPropertyObserver, IDisposable
{
    private readonly GUITickBox _tick;
    private Action<object?>? _changed;
    private GUIImage? _icon;

    public TickElement(ElementContext context)
        : base(new GUITickBox(
            context.Rect(context.Parent, 1f, UiTokens.Percent("control", 0.84f)),
            string.Empty,
            ViewMarkup.FontOf(context.Text("Font"))))
    {
        _tick = (GUITickBox)Control;
        _tick.TextBlock.TextScale = UiMetrics.TextScale;
        _tick.TextBlock.Padding = new Vector4(UiMetrics.Dip(4f), 0f, 0f, 0f);

        _tick.TextBlock.OverflowClip = true;

        _tick.OnSelected = tickBox =>
        {
            _changed?.Invoke(tickBox.Selected);
            return true;
        };

        _tick.RectTransform.SizeChanged += Fit;
        _tick.RectTransform.ScaleChanged += Fit;
    }

    [ElementProperty(KeyText = true)]
    public RichString Text
    {
        set
        {
            _tick.TextBlock.Text = value;
            _tick.TextBlock.TextScale = UiMetrics.TextScale;
            _tick.ToolTip = value;
        }
    }

    [ElementProperty]
    public Sprite? Icon
    {
        set
        {
            if (_icon is null && value is null) { return; }

            _icon ??= CreateIcon();
            _icon.Sprite = value;
            _icon.Visible = value is not null;

            Fit();
        }
    }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public bool Selected { set => _tick.Selected = value; }

    public override void AddContent(ViewElement child) => throw new NotSupportedException("Tick takes no content");

    void IPropertyObserver.Observe(string property, Action<object?> changed)
    {
        if (string.Equals(property, nameof(Selected), StringComparison.OrdinalIgnoreCase)) { _changed = changed; }
    }

    public void Dispose()
    {
        _tick.RectTransform.SizeChanged -= Fit;
        _tick.RectTransform.ScaleChanged -= Fit;
        _tick.OnSelected = null;
        _changed = null;
    }

    private GUIImage CreateIcon()
    {
        GUIImage icon = new(
            new RectTransform(Vector2.One, _tick.layoutGroup.RectTransform, Anchor.CenterLeft, null, null, null, ScaleBasis.BothHeight)
            {
                IsFixedSize = true
            },
            style: null,
            scaleToFit: GUIImage.ScalingMode.ScaleToFitSmallestExtent)
        {
            CanBeFocused = false,

            OverrideState = GUIComponent.ComponentState.None,
            Color = Color.White
        };

        icon.RectTransform.RepositionChildInHierarchy(1);

        return icon;
    }

    private void Fit()
    {
        int width = _tick.Rect.Width;

        if (width <= 0) { return; }

        _tick.ContentWidth = width;

        if (_icon is null) { return; }

        int taken = _tick.box.Rect.Width + (_icon.Visible ? _icon.RectTransform.Rect.Height : 0);

        _tick.TextBlock.RectTransform.RelativeSize = new Vector2(Math.Max(0f, width - taken) / width, 1f);
    }
}
