using System.Linq.Expressions;
using System.Reflection;

namespace UiFramework.Data;

// The compiled reader and writer for one member, cached by declaring type and name: a path segment asks for its
// member every frame its binding is live, and compiling the access once is what keeps that off the list's back.
internal static class MemberAccess
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<(Type Type, string Name), Accessors> _cache = [];

    static MemberAccess() => UiLifetime.Unloading += Clear;

    internal static void Clear() => _cache.Clear();

    internal static Accessors For(Type type, string name)
    {
        if (_cache.TryGetValue((type, name), out Accessors cached)) { return cached; }

        Accessors built = Build(type, name);
        _cache[(type, name)] = built;
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
}

internal readonly record struct Accessors(Func<object, object?>? Read, Action<object, object?>? Write, Type? WriteType);
