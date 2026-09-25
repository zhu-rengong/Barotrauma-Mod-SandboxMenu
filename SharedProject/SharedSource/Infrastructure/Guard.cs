namespace SandboxMenu.Infrastructure;

internal static class Guard
{
    internal static void Run(string what, Action body)
    {
        try
        {
            body();
        }
        catch (Exception e)
        {
            Log.Warn(what, e);
        }
    }

    internal static T Try<T>(string what, Func<T> body, T fallback)
    {
        try
        {
            return body();
        }
        catch (Exception e)
        {
            Log.Warn(what, e);
            return fallback;
        }
    }
}
