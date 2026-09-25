namespace UiFramework;

internal sealed record MarkupDiagnostic(string Message, MarkupNode? Node)
{
    public override string ToString() => Node is null ? Message : $"{Node.File}({Node.Line}): {Message}";
}

internal sealed class MarkupDiagnostics(string view)
{
    private readonly List<MarkupDiagnostic> _items = [];

    internal IReadOnlyList<MarkupDiagnostic> Items => _items;

    internal Action<string>? Sink { get; set; }

    internal void Report(string message, MarkupNode? node = null)
    {
        MarkupDiagnostic item = new(message, node);
        _items.Add(item);

        Sink?.Invoke($"{view}: {item}");
    }

    internal void Flush(Action<string>? sink)
    {
        if (sink is null) { return; }

        foreach (MarkupDiagnostic item in _items)
        {
            sink($"{view}: {item}");
        }

        _items.Clear();
    }
}
