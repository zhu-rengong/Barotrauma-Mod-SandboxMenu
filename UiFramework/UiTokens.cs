using UiFramework.Layout;

namespace UiFramework;

// The sizes the framework's own elements fall back on, named rather than numbered. The mod registers its own set at
// startup and those are what the menu uses; these stay as the defaults for a view built without any registration.
internal static class UiTokens
{
    private static readonly Dictionary<string, UiToken> _defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["row"] = UiToken.Percent(0.075f),
        ["section"] = UiToken.Percent(0.085f),
        ["control"] = UiToken.Percent(0.84f),
        ["labelWidth"] = UiToken.Percent(0.38f),
        ["pad"] = UiToken.Dip(6f),
        ["gap"] = UiToken.Dip(4f),
        ["indent"] = UiToken.Dip(14f),
        ["tile"] = UiToken.Dip(36f)
    };

    private static readonly Dictionary<string, UiToken> _registered = new(StringComparer.OrdinalIgnoreCase);

    static UiTokens() => UiLifetime.Unloading += Reset;

    internal static void Register(IReadOnlyDictionary<string, UiToken> tokens)
    {
        foreach ((string name, UiToken token) in tokens) { _registered[name] = token; }
    }

    internal static void Reset() => _registered.Clear();

    // The fraction a keyword such as "row" stands for.
    internal static float Percent(string name, float fallback)
        => Find(name) is { IsDip: false } token ? token.Value : fallback;

    // The DIP length a keyword such as "pad" stands for.
    internal static float Dip(string name, float fallback)
        => Find(name) is { IsDip: true } token ? token.Value : fallback;

    internal static Length AsLength(string name, Length fallback)
        => Find(name) switch
        {
            { IsDip: true } token => Length.Dip(token.Value),
            { IsDip: false } token => Length.Percent(token.Value),
            _ => fallback
        };

    private static UiToken? Find(string name)
        => _registered.TryGetValue(name, out UiToken token) ? token
            : _defaults.TryGetValue(name, out UiToken fallback) ? fallback
            : null;
}
