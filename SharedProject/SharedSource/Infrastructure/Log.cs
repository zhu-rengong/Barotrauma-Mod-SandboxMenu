namespace SandboxMenu.Infrastructure;

internal static class Log
{
    private const string Prefix = "[SandboxMenu] ";

    static Log() => StaticState.Register(Clear);

    private const double RepeatWindow = 5.0;

    private static readonly Dictionary<string, double> LastSeen = new(StringComparer.Ordinal);

    internal static void Warn(string message) => Write(message, warning: true);

    internal static void Info(string message) => Write(message, warning: false);

    internal static void Clear() => LastSeen.Clear();

    internal static void Warn(string message, Exception exception) => Write($"{message}: {exception.Message}", warning: true);

    private static void Write(string message, bool warning)
    {
        try
        {
            double now = Timing.TotalTime;

            if (LastSeen.TryGetValue(message, out double last) && now - last < RepeatWindow) { return; }

            if (LastSeen.Count > 128) { LastSeen.Clear(); }

            LastSeen[message] = now;

            if (warning)
            {
                DebugConsole.AddWarning(Prefix + message);
            }
            else
            {
                DebugConsole.NewMessage(Prefix + message);
            }
        }
        catch
        {
        }
    }
}
