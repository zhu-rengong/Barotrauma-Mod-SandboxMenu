namespace SandboxMenu.UI.Framework;

internal static class NotificationBatch
{
    private const int MaxPasses = 8;
    private const int BufferLimit = 256;

    private static readonly Dictionary<(object Owner, string Key), Action> _deferred = [];
    private static readonly HashSet<(Notifiable Owner, string? Property)> _pending = [];
    private static readonly List<(Notifiable Owner, string? Property)> _raises = [];
    private static readonly List<(Notifiable Owner, string? Property)> _firing = [];
    private static readonly List<Action> _work = [];

    private static int _depth;
    private static bool _flushing;

    internal static IDisposable Begin()
    {
        _depth++;

        return new Scope();
    }

    internal static bool Enqueue(Notifiable owner, string? property)
    {
        if (_depth == 0) { return false; }

        if (_pending.Add((owner, property))) { _raises.Add((owner, property)); }

        return true;
    }

    internal static bool Defer(object owner, string key, Action work)
    {
        if (_depth == 0) { return false; }

        _deferred[(owner, key)] = work;

        return true;
    }

    private static void Flush()
    {
        if (_flushing) { return; }

        _flushing = true;

        try
        {
            for (int pass = 0; pass < MaxPasses && (_pending.Count > 0 || _deferred.Count > 0); pass++)
            {
                RunDeferred();
                RaisePending();
            }

            RunDeferred();
            RaisePending();
            Trim();
        }
        finally
        {
            _flushing = false;
        }
    }

    private static void RunDeferred()
    {
        if (_deferred.Count == 0) { return; }

        _work.AddRange(_deferred.Values);
        _deferred.Clear();

        for (int i = 0; i < _work.Count; i++)
        {
            FrameActions.Run(_work[i]);
        }

        _work.Clear();
    }

    private static void RaisePending()
    {
        if (_raises.Count == 0) { return; }

        _firing.AddRange(_raises);
        _raises.Clear();
        _pending.Clear();

        for (int i = 0; i < _firing.Count; i++)
        {
            (Notifiable owner, string? property) = _firing[i];
            FrameActions.Run((owner, property), static state => state.owner.RaiseDirect(state.property));
        }

        _firing.Clear();
    }

    private static void Trim()
    {
        if (_raises.Capacity > BufferLimit) { _raises.Capacity = BufferLimit; }
        if (_firing.Capacity > BufferLimit) { _firing.Capacity = BufferLimit; }
        if (_work.Capacity > BufferLimit) { _work.Capacity = BufferLimit; }
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) { return; }

            _disposed = true;
            _depth = Math.Max(0, _depth - 1);

            if (_depth == 0) { Flush(); }
        }
    }
}
