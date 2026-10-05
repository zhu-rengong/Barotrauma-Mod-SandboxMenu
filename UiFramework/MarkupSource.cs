using System.Reflection;
using System.Xml.Linq;

namespace UiFramework;

internal static class MarkupSource
{
    private static readonly Dictionary<string, MarkupNode> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static Assembly? _source;

    static MarkupSource() => UiLifetime.Unloading += Reset;

    // The views are embedded in the mod's assembly, not in this one: the mod names it once at startup.
    internal static void SetSource(Assembly assembly)
    {
        if (ReferenceEquals(_source, assembly)) { return; }

        _source = assembly;
        _cache.Clear();
    }

    internal static MarkupNode Load(string fileName)
        => _cache.TryGetValue(fileName, out MarkupNode? cached)
            ? cached
            : _cache[fileName] = Parse(fileName, ReadFile(fileName));

    private static void Reset()
    {
        _source = null;
        _cache.Clear();
    }

    private static MarkupNode Parse(string view, string markup)
        => MarkupNode.Parse(view, XElement.Parse(markup, LoadOptions.SetLineInfo));

    private static string ReadFile(string fileName)
    {
        Assembly assembly = _source ?? typeof(MarkupSource).Assembly;

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
