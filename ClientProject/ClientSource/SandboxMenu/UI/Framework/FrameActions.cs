namespace SandboxMenu.UI.Framework;

// Menu work that must not run inside the handler that asked for it is posted here and drained with the menu's
// own update; a command that runs where it is asked for goes through Run, which is where its failure ends.
internal static class FrameActions
{
    private static readonly FrameQueue _frame = new();

    static FrameActions() => ModLifetime.Unloading += _frame.Clear;

    internal static void Post(Action action) => _frame.Post(action);

    internal static void Run(Action action) => Guard.Run(action, "Running a menu command failed");

    internal static void Clear() => _frame.Clear();

    internal static void Drain() => _frame.Drain();
}
