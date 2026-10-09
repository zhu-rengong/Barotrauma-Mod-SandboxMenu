namespace SandboxMenu.Domain.Spawning;

internal sealed class SpawnResult
{
    private readonly List<string> _problems = [];

    internal int QueuedCount { get; set; }

    internal IReadOnlyList<string> Problems => _problems;

    internal void Report(string message)
    {
        _problems.Add(message);
        DebugConsole.AddWarning(message);
    }
}
