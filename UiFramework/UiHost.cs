using System.Reflection;

namespace UiFramework;

public static class UiHost
{
    public static void RegisterShortcutHints(string hintKey, string darkHintKey)
    {
        ViewMarkup.HintKey = hintKey;
        ViewMarkup.DarkHintKey = darkHintKey;
    }

    public static void RegisterTokens(IReadOnlyDictionary<string, UiToken> tokens) => UiTokens.Register(tokens);

    public static void RegisterGlobalResources(Styling.ResourceDictionary resources) => Styling.GlobalResources.Register(resources);

    public static void RegisterConverter(string name, Data.IValueConverter converter) => Data.ValueConverters.Register(name, converter);

    public static void RegisterMultiConverter(string name, Data.IMultiValueConverter converter) => Data.ValueConverters.RegisterMulti(name, converter);

    public static void RegisterElementAssembly(Assembly assembly) => ElementRegistry.Register(assembly);

    public static void RegisterViewAssembly(Assembly assembly) => MarkupSource.SetSource(assembly);
}
