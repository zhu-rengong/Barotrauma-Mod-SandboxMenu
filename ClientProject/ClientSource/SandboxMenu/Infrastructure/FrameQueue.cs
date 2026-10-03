namespace SandboxMenu.Infrastructure;

// Work that must not run inside the handler that asked for it — a list rebuilding itself, a command opening a
// dialog — is posted here and drained once per frame.
internal sealed class FrameQueue
{
    private const int MaxPasses = 8;

    private readonly Queue<Action> _posted = [];
    private readonly List<Action> _pass = [];

    private bool _stopped;

    internal void Post(Action action) => _posted.Enqueue(action);

    // What is posted goes, and the pass in flight stops at the action that is running.
    internal void Clear()
    {
        _posted.Clear();
        _stopped = true;
    }

    internal void Drain()
    {
        _stopped = false;

        for (int pass = 0; pass < MaxPasses && _posted.Count > 0; pass++)
        {
            _pass.AddRange(_posted);
            _posted.Clear();

            try
            {
                for (int i = 0; i < _pass.Count && !_stopped; i++) { Guard.Run(_pass[i]); }
            }
            finally
            {
                _pass.Clear();
                _stopped = false;
            }
        }
    }
}
