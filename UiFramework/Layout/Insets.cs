using System.Globalization;

// A layout vocabulary the elements and the builder both spell out, so it lives in the framework's own namespace.
namespace UiFramework;

// One figure per side, in DIP: one value for all four, two for the vertical and the horizontal, or the four the way CSS
// spells them out — top, right, bottom, left.
internal readonly record struct Insets(int Top, int Right, int Bottom, int Left)
{
    internal static Insets None { get; } = new(0, 0, 0, 0);

    internal bool IsEmpty => Top == 0 && Right == 0 && Bottom == 0 && Left == 0;

    internal int Horizontal => Left + Right;

    internal int Vertical => Top + Bottom;

    // The order the host keeps padding in: x and z are the horizontal sides, y and w the vertical ones.
    internal Vector4 ToVector4() => new(Left, Top, Right, Bottom);

    internal static Insets Parse(string? text)
    {
        if (text is not { Length: > 0 }) { return None; }

        string[] parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (1 or 2 or 4)) { return None; }

        int[] values = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];

            if (part.EndsWith("dip", StringComparison.OrdinalIgnoreCase)) { part = part[..^3]; }

            if (!float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) { return None; }

            values[i] = UiMetrics.DipInt(value);
        }

        return values.Length switch
        {
            1 => new Insets(values[0], values[0], values[0], values[0]),
            2 => new Insets(values[0], values[1], values[0], values[1]),
            _ => new Insets(values[0], values[1], values[2], values[3])
        };
    }
}
