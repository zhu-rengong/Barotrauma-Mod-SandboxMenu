namespace SandboxMenu.Domain.Spawning;

internal sealed class SpawnResult
{
    private const int MaxProblems = 8;
    private const int MaxProblemLength = 160;

    private readonly List<string> _problems = [];
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    internal int Limit => SpawnLimits.MaxAmount;

    internal bool IsFull => QueuedCount >= Limit;

    internal int Remaining => Math.Max(0, Limit - QueuedCount);

    internal int QueuedCount { get; set; }

    internal IReadOnlyList<string> Problems => _problems;

    internal void Report(string message)
    {
        if (_problems.Count >= MaxProblems) { return; }

        string limited = message.Length > MaxProblemLength ? message[..MaxProblemLength] : message;

        if (!_seen.Add(limited)) { return; }

        _problems.Add(limited);

        Services.DebugConsole.AddWarning(limited);
    }
}
