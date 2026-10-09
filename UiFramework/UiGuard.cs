namespace UiFramework;

internal static class UiGuard
{
    internal static void Run(Action body, [CallerMemberName] string context = "")
    {
        try
        {
            body();
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"{context}: running the framework failed: {e}");
        }
    }

    internal static void Run<TState>(TState state, Action<TState> body, [CallerMemberName] string context = "")
    {
        try
        {
            body(state);
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"{context}: running the framework failed: {e}");
        }
    }
}
