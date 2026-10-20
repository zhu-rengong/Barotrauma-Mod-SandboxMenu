namespace UiFramework.Controls;

[Element("TextArea")]
internal sealed class TextAreaElement : ViewElement, IPropertyObserver
{
    private readonly GUIListBox _scroll;
    private readonly GUITextBox _box;
    private readonly ViewContext _view;
    private GUITextBox.OnTextChangedHandler? _onTextChanged;
    private string _applied = string.Empty;
    private string? _measuredText;
    private int _measuredWidth = -1;
    private int _contentHeight;
    private float _measuredScale;
    private Vector4 _measuredPadding;
    private int _caretIndex = -1;
    private int _followFrames;

    public TextAreaElement(ElementContext context)
        : base(CreateScroll(context))
    {
        _view = context.View;
        _scroll = (GUIListBox)Control;
        _scroll.HoverCursor = CursorState.Default;
        _scroll.Spacing = 0;

        Insets padding = context.Padding;
        if (!padding.IsEmpty) { _scroll.Padding = padding.ToVector4(); }

        _box = new GUITextBox(
            new RectTransform(Vector2.One, _scroll.Content.RectTransform, Anchor.TopLeft),
            string.Empty,
            textColor: UiMetrics.Text,
            font: ViewMarkup.FontOf(context.Text("Font")),
            textAlignment: Alignment.TopLeft,
            wrap: true,
            style: "GUITextBoxNoBorder",
            color: null,
            createClearButton: false,
            createPenIcon: false)
        {
            CanBeFocused = true
        };

        float inset = UiMetrics.Dip(UiTokens.Dip("pad", 6f));

        _box.Padding = new Vector4(inset, inset * 0.5f, inset, inset * 0.5f);
        _box.TextBlock.TextScale = UiMetrics.TextScale;
        _box.OnEnterPressed = OnEnterPressed;
        _box.RectTransform.SizeChanged += OnBoxSizeChanged;

        _scroll.RectTransform.SizeChanged += Remeasure;
        _scroll.RectTransform.ScaleChanged += Remeasure;

        if (ViewMarkup.ToBool(context.Text("Focus"), false)) { context.View.FocusOnOpen(FocusBox); }

        _view.EveryFrame(FollowCaret);
        _view.Once(Remeasure);
    }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public string? Text
    {
        set
        {
            string text = value ?? string.Empty;
            if (string.Equals(_applied, text, StringComparison.Ordinal)) { return; }

            _applied = text;
            _box.Text = text;
            _box.CaretIndex = 0;
            _scroll.BarScroll = 0f;
            _caretIndex = -1;
            _followFrames = 0;

            Remeasure();
        }
    }

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal string? Font => Node?.Text("Font");

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal bool Focus => ViewMarkup.ToBool(Node?.Text("Focus"), false);

    public override void AddContent(ViewElement child) => throw new NotSupportedException("TextArea takes no content");

    void IPropertyObserver.Observe(string property, Action<object?> changed)
    {
        if (!string.Equals(property, nameof(Text), StringComparison.OrdinalIgnoreCase)) { return; }

        _onTextChanged = (box, text) =>
        {
            _applied = text;
            Remeasure();
            changed(text);
            return true;
        };

        _box.OnTextChanged += _onTextChanged;
    }

    protected override void OnRelease()
    {
        _scroll.RectTransform.SizeChanged -= Remeasure;
        _scroll.RectTransform.ScaleChanged -= Remeasure;
        _box.RectTransform.SizeChanged -= OnBoxSizeChanged;

        StopEveryFrame(FollowCaret);

        if (_onTextChanged is { } handler)
        {
            _box.OnTextChanged -= handler;
            _onTextChanged = null;
        }

        _box.OnEnterPressed = null;

        if (_box.Selected) { _box.Deselect(); }
    }

    private static GUIListBox CreateScroll(ElementContext context)
        => new(context.Rect(context.Parent, 1f, 1f), style: null!)
        {
            HideChildrenOutsideFrame = true,
            SmoothScroll = false,
            AllowMouseWheelScroll = true,
            AutoHideScrollBar = true,
            KeepSpaceForScrollBar = true,
            CanTakeKeyBoardFocus = false,
            CurrentSelectMode = GUIListBox.SelectMode.None
        };

    private bool OnEnterPressed(GUITextBox box, string text)
    {
        box.ReceiveTextInput("\n");
        return true;
    }

    private void FocusBox()
    {
        _scroll.BarScroll = 0f;
        _box.Select(0, ignoreSelectSound: true);
    }

    private void OnBoxSizeChanged() => Remeasure();

    private void Remeasure()
    {
        int width = _scroll.Content.Rect.Width;
        if (width <= 0) { width = Control.Rect.Width; }

        if (width <= 0)
        {
            _view.Once(Remeasure);
            return;
        }

        float scale = MathF.Max(0.01f, _box.TextBlock.TextScale);
        Vector4 padding = _box.Padding;
        string text = _box.Text ?? string.Empty;

        if (_measuredWidth != width
            || !_measuredScale.Equals(scale)
            || !_measuredPadding.Equals(padding)
            || !string.Equals(_measuredText, text, StringComparison.Ordinal))
        {
            _measuredWidth = width;
            _measuredScale = scale;
            _measuredPadding = padding;
            _measuredText = text;

            float wrapWidth = MathF.Max(1f, (width - padding.X - padding.Z) / scale);
            string wrapped = _box.Font.WrapText(text, wrapWidth, out _);

            float height = _box.Font.MeasureString(wrapped, false).Y * scale + padding.Y + padding.W;
            if (text.EndsWith('\n')) { height += _box.Font.LineHeight * scale; }

            _contentHeight = (int)MathF.Ceiling(height);
        }

        Point size = new(width, Math.Max(Math.Max(1, _scroll.Content.Rect.Height), _contentHeight));

        if (_box.RectTransform.NonScaledSize == size) { return; }

        _box.RectTransform.NonScaledSize = size;

        _scroll.ChildrenNeedRecalculation = true;
        _scroll.ScrollBarNeedsRecalculation = true;
        _scroll.UpdateScrollBarSize();
    }

    private void FollowCaret()
    {
        if (!_box.Selected || !ReferenceEquals(GUI.KeyboardDispatcher.Subscriber, _box))
        {
            _caretIndex = -1;
            _followFrames = 0;
            return;
        }

        if (_box.CaretIndex != _caretIndex)
        {
            _caretIndex = _box.CaretIndex;
            _followFrames = 2;
        }

        if (_followFrames <= 0) { return; }

        _followFrames--;

        Rectangle view = _scroll.Content.Rect;
        if (view.Height <= 0) { return; }

        float span = _scroll.TotalSize - view.Height;
        if (span <= 0f) { return; }

        float line = MathF.Max(1f, _box.Font.LineHeight * _box.TextBlock.TextScale);
        float caret = _box.CaretScreenPos.Y;

        if (caret < view.Y)
        {
            _scroll.BarScroll -= (view.Y - caret + line) / span;
            return;
        }

        if (caret > view.Bottom - line)
        {
            _scroll.BarScroll += (caret - view.Bottom + line * 2f) / span;
        }
    }
}
