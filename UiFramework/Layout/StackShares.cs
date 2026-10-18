namespace UiFramework.Layout;

internal static class StackShares
{
    internal static float Fraction(float weight, float total) => total > 0f ? weight / total : 0f;

    internal static float Gaps(int shown, float relativeSpacing, int absoluteSpacing, int extentPixels)
    {
        if (shown < 2) { return 0f; }

        int count = shown - 1;
        float gaps = relativeSpacing * count;

        if (absoluteSpacing > 0 && extentPixels > 0) { gaps += absoluteSpacing * count / (float)extentPixels; }

        return gaps;
    }

    internal static float Remaining(float taken, float gaps) => Math.Max(0f, 1f - taken - gaps);

    internal static int RemainingPixels(int extent, int taken, int gap, int count)
        => Math.Max(0, extent - taken - gap * Math.Max(0, count - 1));

    internal static int PixelsOf(int remaining, float fraction) => (int)MathF.Round(fraction * remaining);
}
