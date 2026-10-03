using System.Runtime.CompilerServices;

namespace SandboxMenu.Infrastructure;

// Failures end where the mod meets the host — hook entries, frame actions, rows being built, property writes;
// anywhere else a failure is a bug left to surface. The context defaults to the calling member.
internal static class Guard
{
    internal static void Run(Action body, [CallerMemberName] string context = "")
    {
        try
        {
            body();
        }
        catch (Exception e)
        {
            Log.Warn(context, e);
        }
    }

    // For a guarded call that needs an argument: handing the argument over here instead of capturing it in a
    // lambda keeps the call free of a closure.
    internal static void Run<TState>(TState state, Action<TState> body, [CallerMemberName] string context = "")
    {
        try
        {
            body(state);
        }
        catch (Exception e)
        {
            Log.Warn(context, e);
        }
    }

    internal static T Try<T>(Func<T> body, T fallback, [CallerMemberName] string context = "")
    {
        try
        {
            return body();
        }
        catch (Exception e)
        {
            Log.Warn(context, e);
            return fallback;
        }
    }

    // Every cleanup has to be carried out: one that fails must not strand the rest (the host reports anything
    // the mod left behind).
    internal static void RunEach(Action? handlers, [CallerMemberName] string context = "")
    {
        if (handlers is null) { return; }

        foreach (Delegate handler in handlers.GetInvocationList()) { Run((Action)handler, context); }
    }
}
