namespace UiFramework.Controls;

internal sealed class TemplatePool<T>(int limit, Action<T> drop) where T : class
{
    private readonly Dictionary<Templating.DataTemplate, LinkedList<T>> _byTemplate = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<LinkedListNode<T>> _order = new();
    private readonly List<LinkedListNode<T>> _live = [];

    private int _count;

    internal int Count => _count;

    internal void Add(Templating.DataTemplate template, T entry)
    {
        if (!_byTemplate.TryGetValue(template, out LinkedList<T>? bucket))
        {
            _byTemplate[template] = bucket = new LinkedList<T>();
        }

        _order.Enqueue(bucket.AddLast(entry));
        _count++;

        while (_count > limit) { Trim(); }

        Compact();
    }

    internal T? Take(Templating.DataTemplate template)
    {
        if (!_byTemplate.TryGetValue(template, out LinkedList<T>? bucket) || bucket.Count == 0) { return null; }

        LinkedListNode<T>? node = bucket.Last;

        if (node is null) { return null; }

        bucket.RemoveLast();
        _count--;

        return node.Value;
    }

    internal void Clear()
    {
        foreach (LinkedList<T> bucket in _byTemplate.Values)
        {
            foreach (T entry in bucket) { drop(entry); }
        }

        _byTemplate.Clear();
        _order.Clear();
        _count = 0;
    }

    private void Trim()
    {
        while (_order.Count > 0)
        {
            LinkedListNode<T>? node = _order.Dequeue();

            if (node?.List is not { } bucket) { continue; }

            bucket.Remove(node);
            _count--;
            drop(node.Value);

            return;
        }

        _count = 0;
    }

    private void Compact()
    {
        if (_order.Count <= limit * 2) { return; }

        _live.Clear();

        foreach (LinkedListNode<T> node in _order)
        {
            if (node.List is not null) { _live.Add(node); }
        }

        _order.Clear();

        for (int i = 0; i < _live.Count; i++) { _order.Enqueue(_live[i]); }

        _live.Clear();
    }
}
