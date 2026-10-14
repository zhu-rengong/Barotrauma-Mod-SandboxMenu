using System.Reflection;

namespace UiFramework;

public static class UiHost
{
    public static void AdvanceFrame(float deltaTime) => Animation.AnimationClock.Advance(deltaTime);

    public static void RegisterShortcutHints(string hintKey, string darkHintKey)
    {
        ViewMarkup.HintKey = hintKey;
        ViewMarkup.DarkHintKey = darkHintKey;
    }

    public static void RegisterTokens(IReadOnlyDictionary<string, UiToken> tokens) => UiTokens.Register(tokens);

    public static void RegisterGlobalResources(ResourceDictionary resources) => GlobalResources.Register(resources);

    public static void RegisterElementAssembly(Assembly assembly) => ElementRegistry.Register(assembly);

    public static void RegisterViewAssembly(Assembly assembly) => MarkupSource.SetSource(assembly);
}
