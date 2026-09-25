namespace SandboxMenu.Infrastructure;

internal static class StaticState
{
    private static readonly List<Action> Clearing = [];

    private static bool _sealed;

    internal static void Register(Action clear)
    {
        // A clear method may itself touch a type that is initializing for the first time (the logger, for
        // instance): once the mod is going away, a late registration would never be cleared, so it is dropped.
        if (_sealed) { return; }

        Clearing.Add(clear);
    }

    internal static void ResetAll()
    {
        _sealed = true;

        Action[] clearing = [.. Clearing];
        Clearing.Clear();

        foreach (Action clear in clearing)
        {
            Guard.Run("Clearing the mod's state failed", clear);
        }
    }
}
