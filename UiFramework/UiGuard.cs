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

    internal static bool TryRun(Func<bool> body, [CallerMemberName] string context = "")
    {
        try
        {
            return body();
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"{context}: running the framework failed: {e}");
            return false;
        }
    }

    internal static bool TryRun<TState>(TState state, Func<TState, bool> body, [CallerMemberName] string context = "")
    {
        try
        {
            return body(state);
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"{context}: running the framework failed: {e}");
            return false;
        }
    }

    internal static void Guard<TState, TValue>(TState state, TValue value, Action<TState, TValue> body, [CallerMemberName] string context = "")
    {
        try
        {
            body(state, value);
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"{context}: running the framework failed: {e}");
        }
    }
}
