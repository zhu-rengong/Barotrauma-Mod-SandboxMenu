using System.Reflection;
using System.Xml.Linq;

namespace UiFramework;

internal static class MarkupSource
{
    private static readonly Dictionary<string, MarkupNode> Cache = new(StringComparer.OrdinalIgnoreCase);

    static MarkupSource() => StaticState.Register(Cache.Clear);

    internal static MarkupNode Load(string fileName)
        => Cache.TryGetValue(fileName, out MarkupNode? cached)
            ? cached
            : Cache[fileName] = Parse(fileName, ReadFile(fileName));

    internal static MarkupNode ParseText(string view, string markup) => Parse(view, markup);

    private static MarkupNode Parse(string view, string markup)
        => MarkupNode.Parse(view, XElement.Parse(markup, LoadOptions.SetLineInfo));

    private static string ReadFile(string fileName)
    {
        Assembly assembly = typeof(MarkupSource).Assembly;

        string resource = assembly.GetManifestResourceNames().FirstOrDefault(name => IsView(name, fileName))
            ?? throw new FileNotFoundException($"View '{fileName}' is not embedded in {assembly.GetName().Name}");

        using Stream stream = assembly.GetManifestResourceStream(resource)
                              ?? throw new FileNotFoundException($"View '{fileName}' cannot be opened");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static bool IsView(string resourceName, string fileName)
        => resourceName.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)
           && (resourceName.Length == fileName.Length || resourceName[^(fileName.Length + 1)] == '.');
}
