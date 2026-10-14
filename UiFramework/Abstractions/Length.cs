namespace UiFramework.Abstractions;

internal enum LengthKind
{
    Auto,

    Star,

    Dip,

    Percent
}

internal readonly record struct Length(LengthKind Kind, float Value)
{
    internal static Length Auto { get; } = new(LengthKind.Auto, 0f);

    internal static Length Fill { get; } = new(LengthKind.Percent, 1f);

    internal static Length Star(float weight = 1f) => new(LengthKind.Star, weight);

    internal static Length Dip(float value) => new(LengthKind.Dip, value);

    internal static Length Percent(float fraction) => new(LengthKind.Percent, fraction);

    internal static bool TryWeight(string? text, out float weight)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Equals("*", StringComparison.Ordinal)) { weight = 1f; return true; }

        if (trimmed.EndsWith('*') && ValueParser.TryFloat(trimmed[..^1], out weight) && weight > 0f) { return true; }

        weight = 0f;
        return false;
    }

    internal static Length Parse(string? text, Length fallback, Action<string>? report = null)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0) { return fallback; }
        if (trimmed.Equals("Fill", StringComparison.OrdinalIgnoreCase)) { return Fill; }
        if (trimmed.Equals("Auto", StringComparison.OrdinalIgnoreCase)) { return Auto; }

        if (trimmed.EndsWith('*'))
        {
            return TryWeight(trimmed, out float weight)
                ? Star(weight)
                : Reject(trimmed, fallback, report);
        }

        if (trimmed.EndsWith('%'))
        {
            return ValueParser.TryPercent(trimmed, out float percent)
                ? Percent(percent)
                : Reject(trimmed, fallback, report);
        }

        if (trimmed.EndsWith("dip", StringComparison.OrdinalIgnoreCase))
        {
            return ValueParser.TryDip(trimmed, out float dip)
                ? Dip(dip)
                : Reject(trimmed, fallback, report);
        }

        if (Keyword(trimmed) is { } keyword) { return keyword; }

        if (!ValueParser.TryFloat(trimmed, out float value))
        {
            return Reject(trimmed, fallback, report);
        }

        if (value > 1f) { report?.Invoke($"'{trimmed}' is a fraction of the panel; write {trimmed}dip for DIP"); }

        return Percent(value);
    }

    internal int Resolve(int available, int measured, float starShare = 1f) => Kind switch
    {
        LengthKind.Auto => measured,
        LengthKind.Star => (int)MathF.Round(starShare * MathF.Max(0f, available)),
        LengthKind.Dip => (int)MathF.Round(UiMetrics.Dip(Value)),
        _ => (int)MathF.Round(Value * available)
    };

    private static Length? Keyword(string text)
    {
        if (text.Equals("row", StringComparison.OrdinalIgnoreCase)) { return UiTokens.AsLength("row", Percent(0.075f)); }
        if (text.Equals("section", StringComparison.OrdinalIgnoreCase)) { return UiTokens.AsLength("section", Percent(0.085f)); }
        if (text.Equals("control", StringComparison.OrdinalIgnoreCase)) { return UiTokens.AsLength("control", Percent(0.84f)); }

        return null;
    }

    private static Length Reject(string text, Length fallback, Action<string>? report)
    {
        report?.Invoke($"'{text}' is not a track size (a fraction, N%, N*, N dip, Auto or Fill)");
        return fallback;
    }
}
