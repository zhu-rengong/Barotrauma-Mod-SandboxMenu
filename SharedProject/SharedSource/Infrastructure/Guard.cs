namespace SandboxMenu.Infrastructure;

internal static class Guard
{
    internal static void Run(string context, Action body)
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

    internal static T Try<T>(string context, Func<T> body, T fallback)
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
}
