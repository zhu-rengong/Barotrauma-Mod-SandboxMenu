using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Reflection;

namespace UiFramework;

internal static class ElementRegistry
{
    private const BindingFlags DeclaredProperties = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly List<Assembly> _sources = [typeof(ElementRegistry).Assembly];
    private static FrozenDictionary<string, ElementMetadata>? _elements;

    internal static FrozenDictionary<string, ElementMetadata> Elements => _elements ??= Scan();

    internal static ElementMetadata? Find(string name) => Elements.GetValueOrDefault(name);

    internal static PropertyMetadata? Property(string element, string name)
        => Find(element)?.Properties.TryGetValue(name, out PropertyMetadata? property) is true ? property : null;

    internal static void Register(Assembly assembly)
    {
        if (_sources.Contains(assembly)) { return; }

        _sources.Add(assembly);
        _elements = null;
    }

    private static FrozenDictionary<string, ElementMetadata> Scan()
    {
        Dictionary<string, ElementMetadata> elements = new(StringComparer.OrdinalIgnoreCase);

        foreach (Assembly source in _sources)
        {
            foreach (Type type in SourceTypes(source))
            {
                if (type.GetCustomAttribute<ElementAttribute>() is not { } element) { continue; }

                try
                {
                    elements[element.Name] = new ElementMetadata(element.Name, FactoryOf(type), PropertiesOf(type));
                }
                catch (Exception e)
                {
                    DebugConsole.AddWarning($"the element <{element.Name}> ({type.FullName}) cannot be used: {e.Message}");
                }
            }
        }

        return elements.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Type> SourceTypes(Assembly source)
    {
        try
        {
            return source.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.OfType<Type>();
        }
    }

    private static Func<ElementContext, ViewElement> FactoryOf(Type type)
    {
        ConstructorInfo constructor = type.GetConstructor([typeof(ElementContext)])
            ?? throw new InvalidOperationException($"Element '{type.Name}' needs a constructor taking ElementContext");

        ParameterExpression context = Expression.Parameter(typeof(ElementContext), "context");
        return Expression.Lambda<Func<ElementContext, ViewElement>>(
            Expression.Convert(Expression.New(constructor, context), typeof(ViewElement)),
            context).Compile();
    }

    private static FrozenDictionary<string, PropertyMetadata> PropertiesOf(Type type)
    {
        Dictionary<string, PropertyMetadata> properties = new(StringComparer.OrdinalIgnoreCase);

        for (Type? current = type; current is not null && typeof(ViewElement).IsAssignableFrom(current); current = current.BaseType)
        {
            foreach (PropertyInfo property in current.GetProperties(DeclaredProperties))
            {
                if (property.GetCustomAttribute<ElementPropertyAttribute>() is not { } declared) { continue; }

                string name = declared.Name ?? property.Name;

                properties.TryAdd(name, new PropertyMetadata(
                    name,
                    property.PropertyType,
                    declared.Phase,
                    declared.Mode,
                    declared.SettableFromStyle,
                    declared.KeyText,
                    WriterOf(property),
                    ReaderOf(property),
                    SnapshotOf(property)));
            }
        }

        return properties.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static Action<object, object?> WriterOf(PropertyInfo property)
    {
        if (property.SetMethod is not { IsPublic: true })
        {
            if (property.GetCustomAttribute<ElementPropertyAttribute>() is { Phase: ApplyPhase.Construct })
            {
                return static (_, _) => { };
            }

            throw new InvalidOperationException($"'{property.Name}' has no public setter");
        }

        return Data.MemberAccess.CompileWriter(property, property.DeclaringType!, property.PropertyType)
            ?? throw new InvalidOperationException($"'{property.Name}' cannot be written");
    }

    private static Func<object, object?>? ReaderOf(PropertyInfo property)
        => property.GetMethod is { IsPublic: true }
            ? Data.MemberAccess.CompileReader(property, property.DeclaringType!)
            : null;

    private static Func<object, long>? SnapshotOf(PropertyInfo property)
    {
        Type type = property.PropertyType;

        if (property.GetMethod is not { IsPublic: true }) { return null; }
        if (type != typeof(bool) && type != typeof(int) && type != typeof(float) && type != typeof(double) && type != typeof(Color)) { return null; }

        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");

        Expression access = Expression.MakeMemberAccess(Expression.Convert(instance, property.DeclaringType!), property);
        Expression body = type == typeof(Color)
            ? Expression.Convert(Expression.Property(access, nameof(Color.PackedValue)), typeof(long))
            : type == typeof(bool)
                ? Expression.Condition(access, Expression.Constant(1L), Expression.Constant(0L))
                : type == typeof(float)
                    ? Expression.Convert(Expression.Call(SingleBits, access), typeof(long))
                    : type == typeof(double)
                        ? Expression.Call(DoubleBits, access)
                        : Expression.Convert(access, typeof(long));

        try
        {
            return Expression.Lambda<Func<object, long>>(body, instance).Compile();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static readonly MethodInfo SingleBits = typeof(BitConverter).GetMethod(nameof(BitConverter.SingleToInt32Bits), [typeof(float)])!;

    private static readonly MethodInfo DoubleBits = typeof(BitConverter).GetMethod(nameof(BitConverter.DoubleToInt64Bits), [typeof(double)])!;
}
