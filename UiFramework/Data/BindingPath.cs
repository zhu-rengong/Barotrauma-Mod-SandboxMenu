using System.ComponentModel;
using System.Globalization;

namespace UiFramework.Data;

// A parsed binding path, cached by its text because the same path is parsed for every row a list builds: the steps
// from the source to the value, and the objects along the way whose changes the binding follows.
internal sealed class BindingPath
{
    private static readonly Dictionary<string, BindingSegment[]> _parseCache = new(StringComparer.Ordinal);

    static BindingPath() => ModLifetime.Unloading += Clear;

    private readonly BindingSegment[] _segments;
    private readonly List<INotifyPropertyChanged> _hooked = [];

    private BindingPath(string text, BindingSegment[] segments)
    {
        Text = text;
        _segments = segments;
    }

    internal string Text { get; }

    internal static void Clear()
    {
        _parseCache.Clear();
        MemberAccess.Clear();
    }

    internal static BindingPath Parse(string text)
    {
        if (_parseCache.TryGetValue(text, out BindingSegment[]? cached)) { return new BindingPath(text, cached); }

        BindingSegment[] parsed = ParseSegments(text);
        _parseCache[text] = parsed;

        return new BindingPath(text, parsed);
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
        Unhook(source, handler);

        object? current = source;
        Track(current, handler);

        foreach (BindingSegment segment in _segments)
        {
            if (current is null) { return; }

            current = segment.Read(current);
            Track(current, handler);
        }
    }

    internal void Unhook(object? source, PropertyChangedEventHandler handler)
    {
        foreach (INotifyPropertyChanged tracked in _hooked)
        {
            tracked.PropertyChanged -= handler;
        }

        _hooked.Clear();
    }

    private void Track(object? current, PropertyChangedEventHandler handler)
    {
        if (current is not INotifyPropertyChanged notify) { return; }

        notify.PropertyChanged += handler;
        _hooked.Add(notify);
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
                    else if (int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int position))
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
