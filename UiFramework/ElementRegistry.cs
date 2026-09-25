using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Reflection;

namespace UiFramework;

internal static class ElementRegistry
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static FrozenDictionary<string, ElementMetadata>? _elements;

    static ElementRegistry() => StaticState.Register(() => _elements = null);

    internal static FrozenDictionary<string, ElementMetadata> Elements => _elements ??= Scan();

    internal static ElementMetadata? Find(string name) => Elements.GetValueOrDefault(name);

    private static FrozenDictionary<string, ElementMetadata> Scan()
    {
        Dictionary<string, ElementMetadata> elements = new(StringComparer.OrdinalIgnoreCase);

        foreach (Type type in typeof(ElementRegistry).Assembly.GetTypes())
        {
            if (type.GetCustomAttribute<ElementAttribute>() is not { } element || type.IsAbstract) { continue; }

            elements[element.Name] = new ElementMetadata(element.Name, FactoryOf(type), PropertiesOf(type));
        }

        return elements.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
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
            foreach (PropertyInfo property in current.GetProperties(PublicInstance))
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
                    WriterOf(property)));
            }
        }

        return properties.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static Action<object, object?> WriterOf(PropertyInfo property)
    {
        if (property.SetMethod is not { IsPublic: true }) { throw new InvalidOperationException($"'{property.Name}' has no setter"); }

        ParameterExpression element = Expression.Parameter(typeof(object), "element");
        ParameterExpression value = Expression.Parameter(typeof(object), "value");

        return Expression.Lambda<Action<object, object?>>(
            Expression.Assign(
                Expression.Property(Expression.Convert(element, property.DeclaringType!), property),
                Expression.Convert(value, property.PropertyType)),
            element,
            value).Compile();
    }
}
