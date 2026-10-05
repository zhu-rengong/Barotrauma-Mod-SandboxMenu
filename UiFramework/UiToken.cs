namespace UiFramework;

// One named size. A fraction of the parent ("row" is 0.075 of the panel) or an absolute DIP length ("pad" is 6 at
// any resolution) — the two ways every size in a view is written.
public readonly record struct UiToken(float Value, bool IsDip)
{
    public static UiToken Percent(float fraction) => new(fraction, false);

    public static UiToken Dip(float dip) => new(dip, true);
}
