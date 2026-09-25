namespace SandboxMenu.UI.Framework;

internal static class MenuActions
{
    private static readonly DelayedQueue Frame = new("Menu action failed");

    static MenuActions() => StaticState.Register(Frame.Clear);

    internal static void Enqueue(Action action) => Frame.Enqueue(action);

    internal static void Run(Action action) => Guard.Run("Menu handler failed", action);

    internal static void Clear() => Frame.Clear();

    internal static void Flush() => Frame.Drain();
}
