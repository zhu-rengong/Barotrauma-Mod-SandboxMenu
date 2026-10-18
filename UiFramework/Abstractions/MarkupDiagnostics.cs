namespace UiFramework.Abstractions;

internal sealed record MarkupDiagnostic(string Message, MarkupNode? Node)
{
    public override string ToString() => Node is null ? Message : $"{Node.View}({Node.Line}): {Message}";
}

internal sealed class MarkupDiagnostics(string view)
{
    private const int Limit = 64;

    private readonly List<MarkupDiagnostic> _items = [];

    internal Action<string>? Sink { get; set; }

    internal void Report(string message, MarkupNode? node = null)
    {
        MarkupDiagnostic item = new(message, node);

        if (Sink is { } sink)
        {
            sink($"{view}: {item}");
            return;
        }

        if (_items.Count < Limit) { _items.Add(item); }
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
