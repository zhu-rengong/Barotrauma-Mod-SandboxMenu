namespace SandboxMenu.Infrastructure;

internal sealed class DelayedQueue(string failure)
{
    private const int MaxPasses = 8;

    private readonly Queue<Action> _pending = [];
    private readonly List<Action> _pass = [];

    private bool _aborted;

    internal void Enqueue(Action action) => _pending.Enqueue(action);

    internal void Clear()
    {
        _pending.Clear();
        _aborted = true;
    }

    internal void Drain()
    {
        _aborted = false;

        for (int pass = 0; pass < MaxPasses && _pending.Count > 0; pass++)
        {
            _pass.AddRange(_pending);
            _pending.Clear();

            try
            {
                for (int i = 0; i < _pass.Count && !_aborted; i++)
                {
                    Guard.Run(failure, _pass[i]);
                }
            }
            finally
            {
                _pass.Clear();
                _aborted = false;
            }
        }
    }
}
