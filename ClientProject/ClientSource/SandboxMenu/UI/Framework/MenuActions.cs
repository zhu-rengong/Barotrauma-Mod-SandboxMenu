namespace SandboxMenu.UI.Framework;

internal static class MenuActions
{
    private static readonly DelayedQueue _frame = new("Menu action failed");

    static MenuActions() => StaticState.Register(_frame.Clear);

    internal static void Enqueue(Action action) => _frame.Enqueue(action);

    internal static void Run(Action action) => Guard.Run("Menu handler failed", action);

    internal static void Clear() => _frame.Clear();

    internal static void Flush() => _frame.Drain();
}
