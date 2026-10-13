using Microsoft.Xna.Framework;
using UiFramework.Layout;

namespace UiFramework.Animation;

internal enum FillBehavior
{
    HoldEnd,
    Stop
}

internal enum RepeatMode
{
    Count,
    Forever
}

internal readonly record struct RepeatBehavior(RepeatMode Mode, float Count)
{
    internal static RepeatBehavior Once { get; } = new(RepeatMode.Count, 1f);

    internal static RepeatBehavior Forever { get; } = new(RepeatMode.Forever, float.PositiveInfinity);

    internal static bool TryParse(string? text, out RepeatBehavior repeat)
    {
        repeat = Once;

        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0) { return false; }

        if (trimmed.Equals("Forever", StringComparison.OrdinalIgnoreCase))
        {
            repeat = Forever;
            return true;
        }

        if (trimmed.EndsWith('x') || trimmed.EndsWith('X')) { trimmed = trimmed[..^1]; }

        if (!float.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float count))
        {
            return false;
        }

        repeat = new RepeatBehavior(RepeatMode.Count, MathF.Max(0f, count));
        return true;
    }

    internal bool IsFinished(int completed)
        => Mode == RepeatMode.Count && completed >= MathF.Max(1f, MathF.Ceiling(Count));
}

internal abstract class AnimationBase
{
    internal required string Property { get; set; }

    internal float Duration { get; set; } = 1f;

    internal float BeginTime { get; set; }

    internal EasingFunction Easing { get; set; } = EasingFunction.Linear;

    internal bool AutoReverse { get; set; }

    internal RepeatBehavior Repeat { get; set; } = RepeatBehavior.Once;

    internal FillBehavior Fill { get; set; } = FillBehavior.HoldEnd;

    internal abstract object? ValueAt(float progress, object? current);

    internal float TotalSeconds => AutoReverse ? Duration * 2f : Duration;
}

internal sealed class DoubleAnimation : AnimationBase
{
    internal double? From { get; init; }

    internal double To { get; init; }

    internal double? By { get; init; }

    internal override object? ValueAt(float progress, object? current)
    {
        float eased = Easing.Ease(progress);
        double start = From ?? AsNumber(current) ?? 0d;
        double end = By is { } by ? start + by : To;

        return start + (end - start) * eased;
    }

    private static double? AsNumber(object? value) => value switch
    {
        double big => big,
        float single => single,
        int number => number,
        _ => null
    };
}

internal sealed class ColorAnimation : AnimationBase
{
    internal Color? From { get; init; }

    internal Color To { get; init; }

    internal override object? ValueAt(float progress, object? current)
        => Lerp(From ?? current as Color? ?? Color.Transparent, To, Easing.Ease(progress));

    internal static Color Lerp(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);

        return new Color(
            (int)MathF.Round(from.R + (to.R - from.R) * t),
            (int)MathF.Round(from.G + (to.G - from.G) * t),
            (int)MathF.Round(from.B + (to.B - from.B) * t),
            (int)MathF.Round(from.A + (to.A - from.A) * t));
    }
}

internal sealed class LengthAnimation : AnimationBase
{
    internal Length? From { get; init; }

    internal Length To { get; init; }

    internal override object? ValueAt(float progress, object? current)
    {
        float eased = Easing.Ease(progress);
        Length start = From ?? current as Length? ?? Length.Fill;

        return start.Kind == To.Kind
            ? new Length(To.Kind, start.Value + (To.Value - start.Value) * eased)
            : eased < 0.5f ? start : To;
    }
}

internal sealed class ThicknessAnimation : AnimationBase
{
    internal Insets? From { get; init; }

    internal Insets To { get; init; }

    internal override object? ValueAt(float progress, object? current)
    {
        float eased = Easing.Ease(progress);
        Insets start = From ?? current as Insets? ?? Insets.None;

        return new Insets(
            Round(start.Top, To.Top, eased),
            Round(start.Right, To.Right, eased),
            Round(start.Bottom, To.Bottom, eased),
            Round(start.Left, To.Left, eased));
    }

    private static int Round(int from, int to, float t) => (int)MathF.Round(from + (to - from) * t);
}

internal readonly record struct KeyFrame<T>(float Time, T Value, EasingFunction Easing);

internal abstract class KeyFramesAnimationBase<T> : AnimationBase
{
    internal IReadOnlyList<KeyFrame<T>> Frames { get; init; } = [];

    protected abstract T Interpolate(T from, T to, float t);

    protected abstract bool TryRead(object? current, out T value);

    protected T Evaluate(float progress, object? current)
    {
        if (Frames.Count == 0) { return default!; }

        float time = Math.Clamp(progress, 0f, 1f) * Duration;
        int last = Frames.Count - 1;

        if (time >= Frames[last].Time) { return Frames[last].Value; }

        if (time < Frames[0].Time)
        {
            return TryRead(current, out T start) && Frames[0].Time > 0f
                ? Interpolate(start, Frames[0].Value, Easing.Ease(time / Frames[0].Time))
                : Frames[0].Value;
        }

        for (int i = 1; i <= last; i++)
        {
            KeyFrame<T> next = Frames[i];

            if (time > next.Time) { continue; }

            KeyFrame<T> previous = Frames[i - 1];
            float span = next.Time - previous.Time;

            if (span <= 0f) { return next.Value; }

            return Interpolate(previous.Value, next.Value, previous.Easing.Ease((time - previous.Time) / span));
        }

        return Frames[last].Value;
    }
}

internal sealed class DoubleAnimationUsingKeyFrames : KeyFramesAnimationBase<double>
{
    protected override double Interpolate(double from, double to, float t) => from + (to - from) * t;

    protected override bool TryRead(object? current, out double value)
    {
        switch (current)
        {
            case double big:
                value = big;
                return true;

            case float single:
                value = single;
                return true;

            case int number:
                value = number;
                return true;

            default:
                value = 0d;
                return false;
        }
    }

    internal override object? ValueAt(float progress, object? current) => Evaluate(progress, current);
}

internal sealed class ColorAnimationUsingKeyFrames : KeyFramesAnimationBase<Color>
{
    protected override Color Interpolate(Color from, Color to, float t) => ColorAnimation.Lerp(from, to, t);

    protected override bool TryRead(object? current, out Color value)
    {
        if (current is Color color)
        {
            value = color;
            return true;
        }

        value = Color.Transparent;
        return false;
    }

    internal override object? ValueAt(float progress, object? current) => Evaluate(progress, current);
}

internal sealed class LengthAnimationUsingKeyFrames : KeyFramesAnimationBase<Length>
{
    protected override Length Interpolate(Length from, Length to, float t)
        => from.Kind == to.Kind ? new Length(to.Kind, from.Value + (to.Value - from.Value) * t) : t < 0.5f ? from : to;

    protected override bool TryRead(object? current, out Length value)
    {
        if (current is Length length)
        {
            value = length;
            return true;
        }

        value = Length.Fill;
        return false;
    }

    internal override object? ValueAt(float progress, object? current) => Evaluate(progress, current);
}

internal sealed class InsetsAnimationUsingKeyFrames : KeyFramesAnimationBase<Insets>
{
    protected override Insets Interpolate(Insets from, Insets to, float t)
        => new(
            (int)MathF.Round(from.Top + (to.Top - from.Top) * t),
            (int)MathF.Round(from.Right + (to.Right - from.Right) * t),
            (int)MathF.Round(from.Bottom + (to.Bottom - from.Bottom) * t),
            (int)MathF.Round(from.Left + (to.Left - from.Left) * t));

    protected override bool TryRead(object? current, out Insets value)
    {
        if (current is Insets insets)
        {
            value = insets;
            return true;
        }

        value = Insets.None;
        return false;
    }

    internal override object? ValueAt(float progress, object? current) => Evaluate(progress, current);
}
