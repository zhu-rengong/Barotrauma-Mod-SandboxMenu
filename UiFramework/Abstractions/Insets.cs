namespace UiFramework.Abstractions;

internal readonly record struct Insets(int Top, int Right, int Bottom, int Left)
{
    internal static Insets None { get; } = new(0, 0, 0, 0);

    internal bool IsEmpty => Top == 0 && Right == 0 && Bottom == 0 && Left == 0;

    internal int Horizontal => Left + Right;

    internal int Vertical => Top + Bottom;

    internal Vector4 ToVector4() => new(Left, Top, Right, Bottom);

    internal static Insets Parse(string? text)
    {
        if (text is not { Length: > 0 }) { return None; }

        Span<int> values = stackalloc int[4];
        int count = 0;
        int start = 0;

        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != ',') { continue; }

            ReadOnlySpan<char> part = text.AsSpan(start, i - start).Trim();
            start = i + 1;

            if (part.Length == 0) { continue; }
            if (count == 4) { return None; }
            if (!ValueParser.TryFloat(part, out float value)) { return None; }

            values[count++] = UiMetrics.DipInt(value);
        }

        return count switch
        {
            1 => new Insets(values[0], values[0], values[0], values[0]),
            2 => new Insets(values[0], values[1], values[0], values[1]),
            4 => new Insets(values[0], values[1], values[2], values[3]),
            _ => None
        };
    }
}
