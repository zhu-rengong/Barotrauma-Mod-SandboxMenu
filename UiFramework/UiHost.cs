using System.Reflection;

namespace UiFramework;

// The one way in: the mod wires the framework up here at startup and takes it down at shutdown. Everything the
// framework cannot know by itself — where it is compiled into, which assembly carries the views, which keys the
// host's shortcuts are written with — arrives through these calls, so nothing in this assembly names the mod.
public static class UiHost
{
    public static void SetLogSink(Action<string, Exception?> sink) => UiLog.Sink = sink;

    // The text keys the host's shortcut hints are written with; without them no hint is drawn.
    public static void RegisterShortcutHints(string hintKey, string darkHintKey)
    {
        ViewMarkup.HintKey = hintKey;
        ViewMarkup.DarkHintKey = darkHintKey;
    }

    // The sizes the markup's keywords stand for ("row", "pad", …).
    public static void RegisterTokens(IReadOnlyDictionary<string, UiToken> tokens) => UiTokens.Register(tokens);

    // The styles every view falls back on, loaded from a markup resource dictionary.
    public static void RegisterGlobalResources(Styling.ResourceDictionary resources) => Styling.GlobalResources.Register(resources);

    // What markup can name in a Converter; the framework's own (Inverse, Percent, Join) are always there.
    public static void RegisterConverter(string name, Data.IValueConverter converter) => Data.ValueConverters.Register(name, converter);

    public static void RegisterMultiConverter(string name, Data.IMultiValueConverter converter) => Data.ValueConverters.RegisterMulti(name, converter);

    // The assembly whose types carry [Element]; the framework's own elements are always scanned.
    public static void RegisterElementAssembly(Assembly assembly) => ElementRegistry.Register(assembly);

    // The assembly whose embedded resources carry the view markup.
    public static void RegisterViewAssembly(Assembly assembly) => MarkupSource.SetSource(assembly);

    public static void Shutdown()
    {
        UiLifetime.Unload();

        UiLog.Sink = null;
        ViewMarkup.HintKey = null;
        ViewMarkup.DarkHintKey = null;
        UiTokens.Reset();
    }
}
