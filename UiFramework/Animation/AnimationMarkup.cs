using System.Globalization;
using Microsoft.Xna.Framework;
using UiFramework.Styling;

namespace UiFramework.Animation;

internal static class AnimationMarkup
{
    internal static Storyboard ReadStoryboard(MarkupNode node, Action<string>? report)
    {
        List<AnimationBase> animations = [];

        foreach (MarkupNode child in node.Content)
        {
            if (ReadAnimation(child, report) is { } animation) { animations.Add(animation); }
        }

        return new Storyboard { Key = node.Text("Key"), Animations = animations };
    }

    internal static AnimationBase? ReadAnimation(MarkupNode node, Action<string>? report)
    {
        string? property = node.Text("Property");

        if (property is not { Length: > 0 })
        {
            Report(node, report, "an animation needs a Property");
            return null;
        }

        AnimationBase? animation = node.Name switch
        {
            "DoubleAnimation" => ReadDouble(node, report, property),
            "ColorAnimation" => ReadColor(node, report, property),
            "LengthAnimation" => ReadLength(node, report, property),
            "ThicknessAnimation" => ReadThickness(node, report, property),
            "DoubleAnimationUsingKeyFrames" => ReadDoubleFrames(node, report, property),
            "ColorAnimationUsingKeyFrames" => ReadColorFrames(node, report, property),
            "LengthAnimationUsingKeyFrames" => ReadLengthFrames(node, report, property),
            "ThicknessAnimationUsingKeyFrames" => ReadThicknessFrames(node, report, property),
            _ => Unknown(node, report)
        };

        if (animation is null) { return null; }

        animation.Duration = Seconds(node.Text("Duration"), 1f, node, report);
        animation.BeginTime = Seconds(node.Text("BeginTime"), 0f, node, report);
        animation.Easing = EasingFunction.TryParse(node.Text("Easing"), out EasingFunction easing) ? easing : EasingFunction.Linear;
        animation.AutoReverse = ViewMarkup.ToBool(node.Text("AutoReverse"), false);
        animation.Repeat = RepeatBehavior.TryParse(node.Text("RepeatBehavior"), out RepeatBehavior repeat) ? repeat : RepeatBehavior.Once;
        animation.Fill = string.Equals(node.Text("FillBehavior"), "Stop", StringComparison.OrdinalIgnoreCase) ? FillBehavior.Stop : FillBehavior.HoldEnd;

        return animation;
    }

    internal static IReadOnlyList<StoryboardAction> ReadActions(MarkupNode node, Action<string>? report)
    {
        List<StoryboardAction> actions = [];

        foreach (MarkupNode child in node.Content)
        {
            switch (child.Name)
            {
                case "BeginStoryboard":
                    MarkupNode body = child.Child("Storyboard") ?? child;

                    if (ReadStoryboard(body, report) is { IsEmpty: false } storyboard)
                    {
                        actions.Add(new BeginStoryboardAction(storyboard));
                    }
                    else
                    {
                        Report(child, report, "a begun storyboard needs at least one animation");
                    }

                    break;

                case "StopStoryboard":
                    actions.Add(new StopStoryboardAction());
                    break;

                case "Set":
                    if (child.Text("Property") is { } setProperty && child.Value("Value") is { } setValue)
                    {
                        actions.Add(new SetAction(new Setter(setProperty, setValue)));
                    }
                    else
                    {
                        Report(child, report, "a Set needs a Property and a Value");
                    }

                    break;

                default:
                    Report(child, report, $"<{child.Name}> is not a trigger action");
                    break;
            }
        }

        return actions;
    }

    internal static float Seconds(string? text, float fallback, MarkupNode node, Action<string>? report)
    {
        if (text is not { Length: > 0 }) { return fallback; }

        string trimmed = text.Trim();

        if (trimmed.Contains(':', StringComparison.Ordinal))
        {
            float total = 0f;

            foreach (string part in trimmed.Split(':'))
            {
                if (!float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    Report(node, report, $"'{text}' is not a duration");
                    return fallback;
                }

                total = total * 60f + value;
            }

            return MathF.Max(0f, total);
        }

        if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
        {
            Report(node, report, $"'{text}' is not a duration (seconds, or h:mm:ss)");
            return fallback;
        }

        return MathF.Max(0f, seconds);
    }

    internal static Color? ReadColor(string? text, MarkupNode node, Action<string>? report, bool allowEmpty)
    {
        if (text is not { Length: > 0 }) { return allowEmpty ? null : Color.Transparent; }

        string trimmed = text.Trim();
        Color color = ViewMarkup.ColorOf(trimmed, Color.Transparent);

        if (color == Color.Transparent
            && !trimmed.Equals("Transparent", StringComparison.OrdinalIgnoreCase)
            && !IsNamedColor(trimmed))
        {
            Report(node, report, $"'{text}' is not a color");
        }

        return color;
    }

    internal static Layout.Length? ReadLength(string? text, MarkupNode node, Action<string>? report, bool allowEmpty)
    {
        if (text is not { Length: > 0 }) { return allowEmpty ? null : Layout.Length.Fill; }

        return Layout.Length.Parse(text, Layout.Length.Fill, message => Report(node, report, message));
    }

    internal static Insets? ReadInsets(string? text, MarkupNode node, Action<string>? report, bool allowEmpty)
    {
        if (text is not { Length: > 0 }) { return allowEmpty ? null : Insets.None; }

        Insets insets = Insets.Parse(text);

        if (insets.IsEmpty) { Report(node, report, $"'{text}' is not a thickness"); }

        return insets;
    }

    internal static void Report(MarkupNode node, Action<string>? report, string message)
        => report?.Invoke($"{node.View}({node.Line}): {message}");

    private static bool IsNamedColor(string text)
        => text.ToLowerInvariant() is "accent" or "bright" or "dim" or "danger" or "disabled" or "callout";

    private static AnimationBase? Unknown(MarkupNode node, Action<string>? report)
    {
        Report(node, report, $"<{node.Name}> is not an animation");
        return null;
    }

    private static AnimationBase? ReadDouble(MarkupNode node, Action<string>? report, string property)
    {
        if (Number(node.Text("To"), node, report) is not { } to)
        {
            Report(node, report, "a double animation needs a To");
            return null;
        }

        return new DoubleAnimation
        {
            Property = property,
            From = Number(node.Text("From"), node, report),
            To = to,
            By = Number(node.Text("By"), node, report)
        };
    }

    private static AnimationBase? ReadColor(MarkupNode node, Action<string>? report, string property)
        => new ColorAnimation
        {
            Property = property,
            From = ReadColor(node.Text("From"), node, report, allowEmpty: true),
            To = ReadColor(node.Text("To"), node, report, allowEmpty: false) ?? Color.Transparent
        };

    private static AnimationBase? ReadLength(MarkupNode node, Action<string>? report, string property)
        => new LengthAnimation
        {
            Property = property,
            From = ReadLength(node.Text("From"), node, report, allowEmpty: true),
            To = ReadLength(node.Text("To"), node, report, allowEmpty: false) ?? Layout.Length.Fill
        };

    private static AnimationBase? ReadThickness(MarkupNode node, Action<string>? report, string property)
        => new ThicknessAnimation
        {
            Property = property,
            From = ReadInsets(node.Text("From"), node, report, allowEmpty: true),
            To = ReadInsets(node.Text("To"), node, report, allowEmpty: false) ?? Insets.None
        };

    private static AnimationBase? ReadDoubleFrames(MarkupNode node, Action<string>? report, string property)
        => new DoubleAnimationUsingKeyFrames
        {
            Property = property,
            Frames = Frames(node, report, static (value, child, inner) => Number(value, child, inner) ?? 0d)
        };

    private static AnimationBase? ReadColorFrames(MarkupNode node, Action<string>? report, string property)
        => new ColorAnimationUsingKeyFrames
        {
            Property = property,
            Frames = Frames(node, report, static (value, child, inner) => ReadColor(value, child, inner, allowEmpty: false) ?? Color.Transparent)
        };

    private static AnimationBase? ReadLengthFrames(MarkupNode node, Action<string>? report, string property)
        => new LengthAnimationUsingKeyFrames
        {
            Property = property,
            Frames = Frames(node, report, static (value, child, inner) => ReadLength(value, child, inner, allowEmpty: false) ?? Layout.Length.Fill)
        };

    private static AnimationBase? ReadThicknessFrames(MarkupNode node, Action<string>? report, string property)
        => new InsetsAnimationUsingKeyFrames
        {
            Property = property,
            Frames = Frames(node, report, static (value, child, inner) => ReadInsets(value, child, inner, allowEmpty: false) ?? Insets.None)
        };

    private static double? Number(string? text, MarkupNode node, Action<string>? report)
    {
        if (text is not { Length: > 0 }) { return null; }

        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            Report(node, report, $"'{text}' is not a number");
            return null;
        }

        return value;
    }

    private static List<KeyFrame<T>> Frames<T>(MarkupNode node, Action<string>? report, Func<string?, MarkupNode, Action<string>?, T> read)
    {
        List<KeyFrame<T>> frames = [];

        foreach (MarkupNode child in node.Content)
        {
            if (child.Text("KeyTime") is not { } keyTime)
            {
                Report(child, report, $"<{child.Name}> needs a KeyTime");
                continue;
            }

            float time = Seconds(keyTime, -1f, child, report);

            if (time < 0f) { continue; }

            string? value = child.Text("Value");

            if (value is null)
            {
                Report(child, report, $"<{child.Name}> needs a Value");
                continue;
            }

            EasingFunction.TryParse(child.Text("Easing"), out EasingFunction easing);

            frames.Add(new KeyFrame<T>(time, read(value, child, report), easing));
        }

        frames.Sort(static (left, right) => left.Time.CompareTo(right.Time));

        return frames;
    }
}
