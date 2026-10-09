namespace SandboxMenu.UI.Framework;

internal static class FrameActions
{
    private const int MaxPasses = 8;

    private static readonly Queue<Action> _posted = [];
    private static readonly List<Action> _pass = [];

    private static bool _stopped;

    internal static void Post(Action action) => _posted.Enqueue(action);

    internal static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Running a menu command failed: {e}");
        }
    }

    internal static void Clear()
    {
        _posted.Clear();
        _stopped = true;
    }

    internal static void Drain()
    {
        _stopped = false;

        for (int pass = 0; pass < MaxPasses && _posted.Count > 0; pass++)
        {
            _pass.AddRange(_posted);
            _posted.Clear();

            try
            {
                for (int i = 0; i < _pass.Count && !_stopped; i++) { Run(_pass[i]); }
            }
            finally
            {
                _pass.Clear();
                _stopped = false;
            }
        }
    }
}
