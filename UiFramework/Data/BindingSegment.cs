using System.Collections;

namespace UiFramework.Data;

// One step of a binding path: the member to read or write and the indices applied to what it gives.
internal readonly record struct BindingSegment(string Name, object[] Indices)
{
    internal object? Read(object source)
    {
        object? value = MemberAccess.For(source.GetType(), Name).Read?.Invoke(source);

        foreach (object index in Indices)
        {
            value = ReadIndex(value, index);
        }

        return value;
    }

    internal bool Write(object source, object? value)
    {
        Accessors accessors = MemberAccess.For(source.GetType(), Name);

        if (accessors.Write is { } writer && accessors.WriteType is { } type)
        {
            (object? converted, bool ok) = ValueConversion.Convert(value, type);
            if (!ok) { return false; }

            writer(source, converted);
            return true;
        }

        return Indices.Length > 0
            && Read(source) is { } indexed
            && WriteIndex(indexed, Indices[^1], value);
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
}
