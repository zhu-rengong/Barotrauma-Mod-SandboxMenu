namespace SandboxMenu.Infrastructure;

internal static class StaticState
{
    private static readonly List<Action> _clearing = [];

    private static bool _sealed;

    internal static void Register(Action clear)
    {
        if (_sealed) { return; }

        _clearing.Add(clear);
    }

    internal static void ResetAll()
    {
        _sealed = true;

        Action[] clearing = [.. _clearing];
        _clearing.Clear();

        foreach (Action clear in clearing)
        {
            Guard.Run("Clearing the mod's state failed", clear);
        }
    }
}
