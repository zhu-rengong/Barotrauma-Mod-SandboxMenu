using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace UiFramework.Data;

internal sealed class BindingPath
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<(Type Type, string Name), Accessors> _accessorCache = [];
    private static readonly Dictionary<string, Segment[]> _parseCache = new(StringComparer.Ordinal);

    static BindingPath() => StaticState.Register(Clear);

    internal static void Clear()
    {
        _accessorCache.Clear();
        _parseCache.Clear();
    }

    private readonly Segment[] _segments;

    private readonly List<INotifyPropertyChanged> _hooked = [];

    private BindingPath(string text, Segment[] segments)
    {
        Text = text;
        _segments = segments;
    }

    internal string Text { get; }

    internal static BindingPath Parse(string text)
    {
        if (_parseCache.TryGetValue(text, out Segment[]? cached)) { return new BindingPath(text, cached); }

        Segment[] parsed = ParseSegments(text);
        _parseCache[text] = parsed;

        return new BindingPath(text, parsed);
    }

    private static Segment[] ParseSegments(string text)
    {
        List<Segment> segments = [];

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

            segments.Add(new Segment(name, [.. indices]));
        }

        return [.. segments];
    }

    internal object? Read(object? source)
    {
        object? current = source;

        foreach (Segment segment in _segments)
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

        foreach (Segment segment in _segments)
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

    private static object? ReadIndex(object? current, object index) => (current, index) switch
    {
        (null, _) => null,
        (IList list, int position) when position >= 0 && position < list.Count => list[position],
        (IDictionary map, _) when map.Contains(index) => map[index],
        _ => null
    };

    private static bool WriteIndex(object current, object index, object? value) => (current, index) switch
    {
        (IList list, int position) when position >= 0 && position < list.Count => Assign(list, position, value),
        (IDictionary map, _) => Assign(map, index, value),
        _ => false
    };

    private static bool Assign(IList list, int position, object? value)
    {
        list[position] = value;
        return true;
    }

    private static bool Assign(IDictionary map, object index, object? value)
    {
        map[index] = value;
        return true;
    }

    private static Accessors AccessorsFor(Type type, string name)
    {
        if (_accessorCache.TryGetValue((type, name), out Accessors cached)) { return cached; }

        Accessors built = Build(type, name);
        _accessorCache[(type, name)] = built;
        return built;
    }

    private static Accessors Build(Type type, string name)
    {
        PropertyInfo? property = FindProperty(type, name);
        FieldInfo? field = property is null ? FindField(type, name) : null;

        MemberInfo? member = (MemberInfo?)property ?? field;
        if (member is null) { return default; }

        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        ParameterExpression value = Expression.Parameter(typeof(object), "value");
        Expression target = Expression.Convert(instance, type);

        Func<object, object?>? reader = null;
        Action<object, object?>? writer = null;

        try
        {
            reader = Expression.Lambda<Func<object, object?>>(
                Expression.Convert(Expression.MakeMemberAccess(target, member), typeof(object)), instance).Compile();
        }
        catch (Exception)
        {
        }

        if (property is { CanWrite: true, SetMethod.IsPublic: true })
        {
            writer = Expression.Lambda<Action<object, object?>>(
                Expression.Assign(Expression.Property(target, property), Expression.Convert(value, property.PropertyType)),
                instance, value).Compile();
        }
        else if (field is { IsInitOnly: false, IsLiteral: false })
        {
            writer = Expression.Lambda<Action<object, object?>>(
                Expression.Assign(Expression.Field(target, field), Expression.Convert(value, field.FieldType)),
                instance, value).Compile();
        }

        return new Accessors(reader, writer, property?.PropertyType ?? field?.FieldType);
    }

    private static PropertyInfo? FindProperty(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            foreach (PropertyInfo property in current.GetProperties(PublicInstance))
            {
                if (property.GetIndexParameters().Length == 0
                    && string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }
        }

        return null;
    }

    private static FieldInfo? FindField(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(PublicInstance))
            {
                if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)) { return field; }
            }
        }

        return null;
    }

    private readonly record struct Segment(string Name, object[] Indices)
    {
        internal object? Read(object source)
        {
            Accessors accessors = AccessorsFor(source.GetType(), Name);
            object? value = accessors.Read?.Invoke(source);

            foreach (object index in Indices)
            {
                value = ReadIndex(value, index);
            }

            return value;
        }

        internal bool Write(object source, object? value)
        {
            Accessors accessors = AccessorsFor(source.GetType(), Name);

            if (accessors.Write is { } writer && accessors.WriteType is { } type)
            {
                (object? converted, bool ok) = ValueReader.Convert(value, type);
                if (!ok) { return false; }

                writer(source, converted);
                return true;
            }

            return Indices.Length > 0
                && Read(source) is { } indexed
                && WriteIndex(indexed, Indices[^1], value);
        }
    }

    private readonly record struct Accessors(Func<object, object?>? Read, Action<object, object?>? Write, Type? WriteType);
}
