using System.Windows.Input;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Controls;

[Element("CharacterTile")]
public sealed class CharacterTileElement : ViewElement, IDisposable, ITileCell
{
    private const float DefaultGrowExtent = 0.36f;

    private readonly RowControl _tile;
    private readonly ViewContext _view;
    private readonly GUICustomComponent _preview;
    private readonly GUITextBlock _name;

    private CharacterDisplay? _display;
    private DeferredSprite? _job;
    private float _grow;
    private float _growExtent = DefaultGrowExtent;
    private float _portraitExtent = Theme.PortraitExtent;
    private float _badgeRatio = Theme.BadgeRatio;
    private Color _textColor = Theme.Text;
    private Color? _badgeColor;
    private bool _showName = true;
    private bool _smallBadge;
    private bool _stretch;
    private CharacterPreviewFraming _framing = CharacterPreviewFraming.Body;
    private bool? _badgeShadow;
    private Color? _badgeShadowColor;
    private int _cellWidth;
    private int _cellHeight;

    public CharacterTileElement(ElementContext context)
        : base(new RowControl(context.Rect(context.Parent, 1f, 1f), string.Empty, Alignment.Center))
    {
        _view = context.View;
        _tile = (RowControl)Control;
        _tile.TextBlock.Visible = false;

        _preview = new GUICustomComponent(new RectTransform(Vector2.One, Control.RectTransform, Anchor.Center), null, null)
        {
            CanBeFocused = false
        };

        _preview.OnDraw = DrawPreview;

        _name = new GUITextBlock(
            new RectTransform(new Vector2(1f, Theme.TileTextRatio), Control.RectTransform, Anchor.BottomCenter),
            string.Empty,
            font: GUIStyle.SmallFont,
            textAlignment: Alignment.Center)
        {
            CanBeFocused = false,
            AutoScaleHorizontal = true,
            OverflowClip = true,
            TextScale = UiMetrics.TextScale,

            HoverColor = Color.Transparent,
            PressedColor = Color.Transparent,
            SelectedColor = Color.Transparent
        };

        Theme.ApplyLabel(_name, _textColor);

        Control.RectTransform.SizeChanged += Layout;

        Layout();
    }

    void ITileCell.SetCellSize(int width, int height)
    {
        _cellWidth = width;
        _cellHeight = height;

        Layout();
    }

    [ElementProperty]
    public CharacterDisplay? Character
    {
        set
        {
            _display = value;
            _name.Text = value?.Name ?? LocalizedString.EmptyString;

            ApplyJobIcon();
            Layout();
        }
    }

    [ElementProperty]
    public bool ShowName
    {
        set
        {
            if (_showName == value) { return; }

            _showName = value;
            _name.Visible = value;

            Layout();
        }
    }

    [ElementProperty]
    public bool SmallBadge
    {
        set
        {
            if (_smallBadge == value) { return; }

            _smallBadge = value;

            ApplyJobIcon();
        }
    }

    [ElementProperty]
    public bool Interactive
    {
        set
        {
            _tile.CanBeSelected = value;
            _tile.Highlight = value;

            if (!value) { _tile.HoverCursor = CursorState.Default; }
        }
    }

    [ElementProperty]
    public string? Framing
    {
        get => _framing == CharacterPreviewFraming.Head ? "Head" : "Body";
        set
        {
            CharacterPreviewFraming framing = string.Equals(value?.Trim(), "Head", StringComparison.OrdinalIgnoreCase)
                ? CharacterPreviewFraming.Head
                : CharacterPreviewFraming.Body;

            _framing = framing;
        }
    }

    [ElementProperty]
    public bool Stretch
    {
        set => _stretch = value;
    }

    [ElementProperty]
    public float Grow
    {
        get => _grow;
        set
        {
            if (Math.Abs(_grow - value) < 0.0001f) { return; }

            _grow = value;

            Layout();
        }
    }

    [ElementProperty]
    public float GrowExtent
    {
        set
        {
            if (Math.Abs(_growExtent - value) < 0.0001f) { return; }

            _growExtent = value;

            Layout();
        }
    }

    [ElementProperty]
    public RichString ToolTip { set => _tile.ToolTip = value; }

    [ElementProperty]
    public Color Fill
    {
        get => _tile.Fill;
        set => _tile.Fill = value;
    }

    [ElementProperty]
    public Color SelectedBarFill
    {
        get => _tile.SelectedBarFill;
        set => _tile.SelectedBarFill = value;
    }

    [ElementProperty]
    public float SelectedBarWidth
    {
        get => _tile.SelectedBarWidth;
        set => _tile.SelectedBarWidth = value;
    }

    [ElementProperty]
    public bool Highlight
    {
        get => _tile.Highlight;
        set => _tile.Highlight = value;
    }

    [ElementProperty]
    public float PortraitExtent
    {
        set
        {
            _portraitExtent = value;
            Layout();
        }
    }

    [ElementProperty]
    public float BadgeRatio
    {
        set
        {
            _badgeRatio = value;
            Layout();
        }
    }

    [ElementProperty]
    public Color BadgeColor
    {
        set
        {
            _badgeColor = value;

            if (_job is not null) { _job.Image.Color = value; }
        }
    }

    [ElementProperty]
    public Color TextColor
    {
        set
        {
            _textColor = value;
            Theme.ApplyLabel(_name, value);
        }
    }

    [ElementProperty]
    public bool IconShadow
    {
        get => _badgeShadow ?? true;
        set
        {
            _badgeShadow = value;

            if (_job is not null) { _job.Shadow = value; }
        }
    }

    [ElementProperty]
    public Color IconShadowColor
    {
        set
        {
            _badgeShadowColor = value;

            if (_job is not null) { _job.ShadowColor = value; }
        }
    }

    [ElementProperty]
    public ICommand? Command
    {
        set => _tile.OnClicked = (_, _) =>
        {
            RaiseEvent("Click");
            _view.Dispatch(() => value?.Execute(null));

            return false;
        };
    }

    public void Dispose()
    {
        Control.RectTransform.SizeChanged -= Layout;

        _tile.OnClicked = null;

        if (_preview is not null) { _preview.OnDraw = null; }

        _job?.Dispose();
        _job = null;
        _display = null;
    }

    private void ApplyJobIcon()
    {
        if (_display is not { } display)
        {
            _job?.Set(null);
            return;
        }

        if ((_smallBadge ? display.JobSmallIcon : display.JobIcon) is not { } icon)
        {
            _job?.Set(null);
            return;
        }

        DeferredSprite job = Job();

        job.Image.Color = _badgeColor ?? display.JobColor;
        job.Set(icon);
    }

    private DeferredSprite Job()
    {
        if (_job is { } job) { return job; }

        job = new DeferredSprite(
            new RectTransform(Vector2.One, Control.RectTransform, Anchor.TopLeft, Pivot.TopLeft),
            GUIImage.ScalingMode.ScaleToFitSmallestExtent);

        if (_badgeShadow is { } shadow) { job.Shadow = shadow; }
        if (_badgeShadowColor is { } shadowColor) { job.ShadowColor = shadowColor; }

        return _job = job;
    }

    private void DrawPreview(SpriteBatch spriteBatch, GUICustomComponent component)
    {
        if (_display is not { } display) { return; }

        Rectangle rect = component.Rect;

        if (rect.Width <= 0 || rect.Height <= 0) { return; }

        CharacterPreview preview = _framing == CharacterPreviewFraming.Head ? display.PreviewHead : display.Preview;

        preview.Request();

        if (preview.Texture is not { } texture) { return; }

        Rectangle target;
        Rectangle? source = null;

        if (_stretch && rect.Width <= 256 * rect.Height && rect.Height <= 256 * rect.Width)
        {
            float cover = Math.Max(rect.Width / (float)texture.Width, rect.Height / (float)texture.Height);
            int width = Math.Min(texture.Width, Math.Max(1, (int)MathF.Round(rect.Width / cover)));
            int height = Math.Min(texture.Height, Math.Max(1, (int)MathF.Round(rect.Height / cover)));

            source = new Rectangle((texture.Width - width) / 2, (texture.Height - height) / 2, width, height);
            target = rect;
        }
        else
        {
            float scale = Math.Min(rect.Width / (float)texture.Width, rect.Height / (float)texture.Height);

            target = new Rectangle(0, 0, (int)(texture.Width * scale), (int)(texture.Height * scale));
            target.X = rect.Center.X - target.Width / 2;
            target.Y = rect.Center.Y - target.Height / 2;
        }

        try
        {
            if (source is { } crop) { spriteBatch.Draw(texture, target, crop, Color.White); }
            else { spriteBatch.Draw(texture, target, Color.White); }
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Drawing a character preview failed: {e}");
        }
    }

    private void Layout()
    {
        Rectangle rect = Control.Rect;
        int width = Math.Max(1, _cellWidth > 0 ? _cellWidth : rect.Width);
        int height = Math.Max(1, _cellHeight > 0 ? _cellHeight : rect.Height);

        int nameHeight = _showName ? Math.Max(1, (int)MathF.Round(height * Theme.TileTextRatio)) : 0;
        Place(_name.RectTransform, new Point(width, nameHeight), Point.Zero, Anchor.BottomCenter);

        float grown = 1f + _grow * _growExtent;
        int area = Math.Max(1, height - nameHeight);
        int side = Math.Max(1, (int)MathF.Round(Math.Min(width, area) * _portraitExtent * grown));

        Place(
            _preview.RectTransform,
            _stretch ? new Point(width, area) : new Point(side, side),
            new Point(0, -nameHeight / 2),
            Anchor.Center);

        if (_job is not { } job) { return; }

        int badge = Math.Max(1, (int)MathF.Round(Math.Min(width, height) * _badgeRatio));
        int pad = UiMetrics.DipInt(Theme.SlotPadding);

        Place(job.Transform, new Point(badge, badge), new Point(pad, pad), Anchor.TopLeft);
        job.Refit();
    }

    private static void Place(RectTransform transform, Point size, Point offset, Anchor anchor)
    {
        transform.SetPosition(anchor);
        transform.IsFixedSize = true;
        transform.NonScaledSize = new Point(Math.Max(1, size.X), Math.Max(1, size.Y));
        transform.AbsoluteOffset = offset;
    }
}
