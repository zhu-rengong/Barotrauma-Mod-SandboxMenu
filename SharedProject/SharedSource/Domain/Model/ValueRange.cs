namespace SandboxMenu.Domain.Model;

internal readonly record struct ValueRange
{
    public ValueRange(float value)
    {
        Min = value;
        Max = value;
    }

    public ValueRange(float min, float max)
    {
        if (min > max) { (min, max) = (max, min); }
        Min = min;
        Max = max;
    }

    public float Min { get; init; }

    public float Max { get; init; }

    public bool IsRange => Max > Min;

    public float Roll()
        => IsRange ? Min + (float)Random.Shared.NextDouble() * (Max - Min) : Min;

    public float Roll(bool round)
        => round ? MathF.Round(Roll(), MidpointRounding.AwayFromZero) : Roll();

    public override string ToString()
        => IsRange ? $"{Min:0.##}~{Max:0.##}" : Min.ToString("0.##");
}
