using System.Linq.Expressions;
using System.Reflection;

namespace UiFramework.Data;

internal static class MemberAccess
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<(Type Type, string Name), Accessors> _cache = [];

    internal static Accessors For(Type type, string name)
    {
        if (_cache.TryGetValue((type, name), out Accessors cached)) { return cached; }

        Accessors built = Build(type, name);
        _cache[(type, name)] = built;
        return built;
    }

    internal static Func<object, object?>? CompileReader(MemberInfo member, Type declaringType)
    {
        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");

        try
        {
            return Expression.Lambda<Func<object, object?>>(
                Expression.Convert(
                    Expression.MakeMemberAccess(Expression.Convert(instance, declaringType), member),
                    typeof(object)),
                instance).Compile();
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static Action<object, object?>? CompileWriter(MemberInfo member, Type declaringType, Type valueType)
    {
        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        ParameterExpression value = Expression.Parameter(typeof(object), "value");

        try
        {
            return Expression.Lambda<Action<object, object?>>(
                Expression.Assign(
                    Expression.MakeMemberAccess(Expression.Convert(instance, declaringType), member),
                    Expression.Convert(value, valueType)),
                instance,
                value).Compile();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Accessors Build(Type type, string name)
    {
        PropertyInfo? property = FindProperty(type, name);
        FieldInfo? field = property is null ? FindField(type, name) : null;

        MemberInfo? member = (MemberInfo?)property ?? field;
        if (member is null) { return default; }

        Func<object, object?>? reader = CompileReader(member, type);
        Action<object, object?>? writer = null;

        if (property is { CanWrite: true, SetMethod.IsPublic: true })
        {
            writer = CompileWriter(property, type, property.PropertyType);
        }
        else if (field is { IsInitOnly: false, IsLiteral: false })
        {
            writer = CompileWriter(field, type, field.FieldType);
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
