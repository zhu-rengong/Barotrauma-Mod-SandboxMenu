namespace UiFramework;

// The framework has no console of its own: the mod hands it a sink at startup (its own Log.Warn) and takes it back
// at shutdown, so nothing in here holds a delegate into the mod once the plugin is unloaded.
internal static class UiLog
{
    internal static Action<string, Exception?>? Sink { get; set; }

    internal static void Warn(string message) => Write(message, null);

    internal static void Warn(string message, Exception exception) => Write($"{message}: {exception}", null);

    internal static void Warn(string message, Exception? exception, string context)
        => Write(string.IsNullOrEmpty(context) ? message : $"{context}: {message}", exception);

    private static void Write(string message, Exception? exception)
    {
        if (Sink is not { } sink) { return; }

        try
        {
            sink(message, exception);
        }
        catch
        {
        }
    }
}
