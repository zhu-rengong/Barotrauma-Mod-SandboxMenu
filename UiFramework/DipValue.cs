namespace UiFramework;

public struct DipValue
{
    private float _dip;
    private float _scale;
    private int _pixels;
    private bool _has;

    public int Pixels(float dip)
    {
        float scale = UiMetrics.UiScale;

        if (_has && _dip == dip && _scale == scale) { return _pixels; }

        _dip = dip;
        _scale = scale;
        _pixels = UiMetrics.DipInt(dip);
        _has = true;

        return _pixels;
    }
}
