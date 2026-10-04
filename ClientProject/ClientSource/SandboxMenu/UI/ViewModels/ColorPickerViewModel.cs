namespace SandboxMenu.UI.ViewModels;

// The colour a picker window is working on. The picker reports whole colours and each field reports a single number,
// so the colour is kept as hue, saturation, brightness and alpha: a field only rewrites the part it stands for, and
// the other fields follow whatever moved.
internal sealed class ColorPickerViewModel : Notifiable
{
    private readonly Action<Color> _onPicked;

    private float _hue;
    private float _saturation = 1f;
    private float _brightness = 1f;
    private int _alpha = 255;
    private string _hex = "#FFFFFF";
    private bool _updating;

    internal ColorPickerViewModel(Color current, Action<Color> onPicked)
    {
        _onPicked = onPicked;
        Picked = color => Apply(new Color(color.R, color.G, color.B, _alpha), deriveHsv: true, report: true);

        Apply(current, deriveHsv: true, report: false);
    }

    // What the picker element calls for every colour dragged out of it. The picker carries no alpha of its own, so the
    // one the field holds is what goes back on.
    public Action<Color> Picked { get; }

    public Color Color => Compose();

    public float Hue
    {
        get => _hue;
        set => FromField(() => _hue = Math.Clamp(value, 0f, 360f), deriveHsv: false);
    }

    public float Saturation
    {
        get => _saturation;
        set => FromField(() => _saturation = Math.Clamp(value, 0f, 1f), deriveHsv: false);
    }

    public float Brightness
    {
        get => _brightness;
        set => FromField(() => _brightness = Math.Clamp(value, 0f, 1f), deriveHsv: false);
    }

    public float Red
    {
        get => Compose().R;
        set => FromField(() => DeriveHsv(new Color(ToByte(value), Compose().G, Compose().B, _alpha)), deriveHsv: false);
    }

    public float Green
    {
        get => Compose().G;
        set => FromField(() => DeriveHsv(new Color(Compose().R, ToByte(value), Compose().B, _alpha)), deriveHsv: false);
    }

    public float Blue
    {
        get => Compose().B;
        set => FromField(() => DeriveHsv(new Color(Compose().R, Compose().G, ToByte(value), _alpha)), deriveHsv: false);
    }

    public float Alpha
    {
        get => _alpha;
        set => FromField(() => _alpha = ToByte(value), deriveHsv: false);
    }

    // Typed colours are taken as they are written, but only while what is written looks like one: a half-finished
    // "#F" would otherwise land on some colour the player never asked for. The text itself is left as typed.
    public string Hex
    {
        get => _hex;
        set
        {
            if (_updating || value is not { } text || !LooksLikeColor(text)) { return; }

            _hex = text;
            Apply(XMLExtensions.ParseColor(text, false), deriveHsv: true, report: true);
        }
    }

    // Once the edit is over the box shows the colour that stands: what was typed and not taken gives way to it, and a
    // colour is written the one way the host reads.
    public Action Commit => Refresh;

    private void FromField(Action change, bool deriveHsv)
    {
        if (_updating) { return; }

        change();
        Apply(Compose(), deriveHsv, report: true);
    }

    private void Apply(Color color, bool deriveHsv, bool report)
    {
        if (deriveHsv) { DeriveHsv(color); }

        _alpha = color.A;
        _hex = color.ToStringHex();

        Refresh();

        if (report) { _onPicked?.Invoke(Compose()); }
    }

    private void DeriveHsv(Color color)
    {
        Vector3 hsv = ToolBox.RGBToHSV(color);

        _hue = float.IsNaN(hsv.X) ? 0f : hsv.X;
        _saturation = hsv.Y;
        _brightness = hsv.Z;
    }

    private Color Compose()
    {
        Color rgb = ToolBoxCore.HSVToRGB(_hue, _saturation, _brightness);

        return new Color(rgb.R, rgb.G, rgb.B, _alpha);
    }

    // One raise is what the window needs: a binding re-reads its own path when the source it is hooked to reports a
    // change. The fields are kept from writing back while that runs, or every value pushed into a field would be read
    // as an edit and set the rest off in turn.
    private void Refresh()
    {
        _updating = true;

        try { Raise(); }
        finally { _updating = false; }
    }

    private static bool LooksLikeColor(string text)
        => text.StartsWith('#') || text.Contains(',');

    private static int ToByte(float value) => (int)Math.Clamp(MathF.Round(value), 0f, 255f);
}
