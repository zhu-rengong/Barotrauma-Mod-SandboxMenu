using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;

namespace UiFramework.Data;

internal sealed class BindingPath
{
    private static readonly ConcurrentDictionary<string, BindingSegment[]> _parseCache = new(StringComparer.Ordinal);

    private readonly BindingSegment[] _segments;
    private readonly List<INotifyPropertyChanged> _hooked = [];
    private readonly List<INotifyPropertyChanged> _scratch = [];

    private BindingPath(string text, BindingSegment[] segments)
    {
        Text = text;
        _segments = segments;
    }

    internal string Text { get; }

    internal static BindingPath Parse(string text)
    {
        if (_parseCache.TryGetValue(text, out BindingSegment[]? cached)) { return new BindingPath(text, cached); }

        return new BindingPath(text, _parseCache.GetOrAdd(text, static key => ParseSegments(key)));
    }

    internal object? Read(object? source)
    {
        object? current = source;

        foreach (BindingSegment segment in _segments)
        {
            if (current is null) { return null; }

            current = segment.Read(current);
        }

        return current;
    }

    internal bool Write(object? source, object? value)
    {
        object? current = source;

        for (int i = 0; i < _segments.Length; i++)
        {
            if (current is null) { return false; }

            if (i == _segments.Length - 1)
            {
                return _segments[i].Write(current, value);
            }

            current = _segments[i].Read(current);
        }

        return false;
    }

    internal void Hook(object? source, PropertyChangedEventHandler handler)
    {
        _scratch.Clear();

        object? current = source;
        Track(current);

        for (int i = 0; i < _segments.Length; i++)
        {
            if (current is null) { break; }

            current = _segments[i].Read(current);
            Track(current);
        }

        if (SameChain()) { return; }

        Unhook(source, handler);

        for (int i = 0; i < _scratch.Count; i++) { _scratch[i].PropertyChanged += handler; }

        _hooked.Clear();
        _hooked.AddRange(_scratch);
    }

    internal void Unhook(object? source, PropertyChangedEventHandler handler)
    {
        foreach (INotifyPropertyChanged tracked in _hooked)
        {
            tracked.PropertyChanged -= handler;
        }

        _hooked.Clear();
    }

    private void Track(object? current)
    {
        if (current is not INotifyPropertyChanged notify) { return; }

        _scratch.Add(notify);
    }

    private bool SameChain()
    {
        if (_hooked.Count != _scratch.Count) { return false; }

        for (int i = 0; i < _hooked.Count; i++)
        {
            if (!ReferenceEquals(_hooked[i], _scratch[i])) { return false; }
        }

        return true;
    }

    private static BindingSegment[] ParseSegments(string text)
    {
        List<BindingSegment> segments = [];

        foreach (string part in text.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string name = part;
            List<object> indices = [];

            int bracket = part.IndexOf('[', StringComparison.Ordinal);
            if (bracket >= 0)
            {
                name = part[..bracket];

                foreach (string raw in part[bracket..].Split('[', StringSplitOptions.RemoveEmptyEntries))
                {
                    string index = raw.TrimEnd(']');
                    if (index.Length == 0) { continue; }

                    if (index.Length >= 2 && index.StartsWith('"') && index.EndsWith('"'))
                    {
                        indices.Add(index[1..^1]);
                    }
                    else if (ValueParser.TryInt(index, out int position))
                    {
                        indices.Add(position);
                    }
                    else
                    {
                        indices.Add(index);
                    }
                }
            }

            segments.Add(new BindingSegment(name, [.. indices]));
        }

        return [.. segments];
    }
}
