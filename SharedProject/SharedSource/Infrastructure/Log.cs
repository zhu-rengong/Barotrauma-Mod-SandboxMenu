using System.Collections.Concurrent;

namespace SandboxMenu.Infrastructure;

internal static class Log
{
    private const string Prefix = "[SandboxMenu] ";
    private const double RepeatWindow = 5.0;
    private const int MaxEntries = 128;

    private static readonly ConcurrentDictionary<string, double> _lastSeen = new(StringComparer.Ordinal);

    static Log() => ModLifetime.Unloading += Clear;

    internal static void Warn(string message) => Write(message, warning: true);

    internal static void Warn(string message, Exception exception) => Write($"{message}: {exception}", warning: true);

    internal static void Info(string message) => Write(message, warning: false);

    internal static void Clear() => _lastSeen.Clear();

    private static void Write(string message, bool warning)
    {
        try
        {
            double now = Timing.TotalTime;

            if (_lastSeen.TryGetValue(message, out double last) && now - last < RepeatWindow) { return; }

            if (_lastSeen.Count > MaxEntries) { _lastSeen.Clear(); }

            _lastSeen[message] = now;

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
