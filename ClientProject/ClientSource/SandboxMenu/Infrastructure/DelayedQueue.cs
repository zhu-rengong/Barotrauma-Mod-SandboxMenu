namespace SandboxMenu.Infrastructure;

internal sealed class DelayedQueue(string failure)
{
    private const int MaxPasses = 8;

    private readonly Queue<Action> _pending = [];
    private readonly List<Action> _pass = [];

    internal void Enqueue(Action action) => _pending.Enqueue(action);

    internal void Clear() => _pending.Clear();

    internal void Drain()
    {
        // Work queued while draining (a rebuild triggered by another rebuild) is settled in the same frame
        // instead of waiting for the next one.
        for (int pass = 0; pass < MaxPasses && _pending.Count > 0; pass++)
        {
            _pass.AddRange(_pending);
            _pending.Clear();

            try
            {
                for (int i = 0; i < _pass.Count; i++)
                {
                    Guard.Run(failure, _pass[i]);
                }
            }
            finally
            {
                _pass.Clear();
            }
        }
    }
}
