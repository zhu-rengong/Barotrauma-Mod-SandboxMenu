namespace UiFramework;

public sealed class NameScope
{
    private readonly Dictionary<string, ViewElement> _elements = new(StringComparer.Ordinal);

    internal void Add(string name, ViewElement element, MarkupDiagnostics diagnostics, MarkupNode node)
    {
        if (_elements.TryAdd(name, element)) { return; }

        diagnostics.Report($"the name '{name}' is used twice", node);
    }

    public ViewElement? Find(string name) => _elements.GetValueOrDefault(name);
}
