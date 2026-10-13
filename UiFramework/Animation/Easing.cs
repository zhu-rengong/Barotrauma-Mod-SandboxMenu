namespace UiFramework.Animation;

internal enum EasingKind
{
    Linear,
    Sine,
    Quad,
    Cubic,
    Quart,
    Quint,
    Expo,
    Circ,
    Back,
    Bounce,
    Elastic
}

internal enum EasingMode
{
    In,
    Out,
    InOut
}

internal readonly record struct EasingFunction(EasingKind Kind, EasingMode Mode)
{
    internal static EasingFunction Linear { get; } = new(EasingKind.Linear, EasingMode.Out);

    internal float Ease(float t)
    {
        float clamped = Math.Clamp(t, 0f, 1f);

        return Kind switch
        {
            EasingKind.Linear => clamped,
            EasingKind.Sine => Sine(clamped),
            EasingKind.Quad => Power(clamped, 2f),
            EasingKind.Cubic => Power(clamped, 3f),
            EasingKind.Quart => Power(clamped, 4f),
            EasingKind.Quint => Power(clamped, 5f),
            EasingKind.Expo => Expo(clamped),
            EasingKind.Circ => Circ(clamped),
            EasingKind.Back => Back(clamped),
            EasingKind.Bounce => Mode == EasingMode.In ? 1f - Bounce(1f - clamped) : Bounce(clamped),
            _ => Elastic(clamped)
        };
    }

    internal static bool TryParse(string? text, out EasingFunction function)
    {
        function = Linear;

        if (text is not { Length: > 0 }) { return false; }

        string trimmed = text.Trim();

        EasingMode mode = EasingMode.Out;
        EasingKind kind;

        if (trimmed.EndsWith("InOut", StringComparison.OrdinalIgnoreCase))
        {
            mode = EasingMode.InOut;
            trimmed = trimmed[..^5];
        }
        else if (trimmed.EndsWith("Out", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 3)
        {
            mode = EasingMode.Out;
            trimmed = trimmed[..^3];
        }
        else if (trimmed.EndsWith("In", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 2)
        {
            mode = EasingMode.In;
            trimmed = trimmed[..^2];
        }

        if (!Enum.TryParse(trimmed, ignoreCase: true, out kind)) { return false; }

        function = new EasingFunction(kind, mode);
        return true;
    }

    private float Power(float t, float exponent) => Mode switch
    {
        EasingMode.In => MathF.Pow(t, exponent),
        EasingMode.Out => 1f - MathF.Pow(1f - t, exponent),
        _ => t < 0.5f ? MathF.Pow(t * 2f, exponent) / 2f : 1f - MathF.Pow(2f - t * 2f, exponent) / 2f
    };

    private float Sine(float t) => Mode switch
    {
        EasingMode.In => 1f - MathF.Cos(t * MathF.PI / 2f),
        EasingMode.Out => MathF.Sin(t * MathF.PI / 2f),
        _ => -(MathF.Cos(MathF.PI * t) - 1f) / 2f
    };

    private float Expo(float t) => Mode switch
    {
        EasingMode.In => t <= 0f ? 0f : MathF.Pow(2f, 10f * t - 10f),
        EasingMode.Out => t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t),
        _ => t <= 0f ? 0f
            : t >= 1f ? 1f
            : t < 0.5f ? MathF.Pow(2f, 20f * t - 10f) / 2f
            : (2f - MathF.Pow(2f, -20f * t + 10f)) / 2f
    };

    private float Circ(float t) => Mode switch
    {
        EasingMode.In => 1f - MathF.Sqrt(Math.Max(0f, 1f - t * t)),
        EasingMode.Out => MathF.Sqrt(Math.Max(0f, 1f - (t - 1f) * (t - 1f))),
        _ => t < 0.5f
            ? (1f - MathF.Sqrt(Math.Max(0f, 1f - 4f * t * t))) / 2f
            : (MathF.Sqrt(Math.Max(0f, 1f - (2f * t - 2f) * (2f * t - 2f))) + 1f) / 2f
    };

    private float Back(float t)
    {
        const float overshoot = 1.70158f;

        return Mode switch
        {
            EasingMode.In => (overshoot + 1f) * t * t * t - overshoot * t * t,
            EasingMode.Out => 1f + (overshoot + 1f) * MathF.Pow(t - 1f, 3f) + overshoot * MathF.Pow(t - 1f, 2f),
            _ => t < 0.5f
                ? MathF.Pow(2f * t, 2f) * ((overshoot * 1.525f + 1f) * 2f * t - overshoot * 1.525f) / 2f
                : (MathF.Pow(2f * t - 2f, 2f) * ((overshoot * 1.525f + 1f) * (t * 2f - 2f) + overshoot * 1.525f) + 2f) / 2f
        };
    }

    private float Elastic(float t)
    {
        const float period = MathF.Tau / 3f;

        return Mode switch
        {
            EasingMode.In => t <= 0f ? 0f : t >= 1f ? 1f : -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * period),
            EasingMode.Out => t <= 0f ? 0f : t >= 1f ? 1f : MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * period) + 1f,
            _ => t <= 0f ? 0f
                : t >= 1f ? 1f
                : t < 0.5f
                    ? -(MathF.Pow(2f, 20f * t - 10f) * MathF.Sin((20f * t - 11.125f) * (MathF.Tau / 4.5f))) / 2f
                    : (MathF.Pow(2f, -20f * t + 10f) * MathF.Sin((20f * t - 11.125f) * (MathF.Tau / 4.5f))) / 2f + 1f
        };
    }

    private static float Bounce(float t)
    {
        const float n = 7.5625f;
        const float d = 2.75f;

        if (t < 1f / d) { return n * t * t; }
        if (t < 2f / d) { return n * (t -= 1.5f / d) * t + 0.75f; }
        if (t < 2.5f / d) { return n * (t -= 2.25f / d) * t + 0.9375f; }

        return n * (t -= 2.625f / d) * t + 0.984375f;
    }
}
