using Barotrauma.Items.Components;

namespace SandboxMenu.Domain.Editing;

internal readonly record struct AppliedOverride(ISerializableEntity Target, SerializableProperty Property);

internal static class ItemPropertyWriter
{
    internal static IReadOnlyList<string> Apply(
        Item item,
        IReadOnlyList<PropertyOverride> overrides,
        ICollection<AppliedOverride>? applied = null)
    {
        if (overrides.Count == 0) { return []; }

        List<string>? problems = null;

        foreach (PropertyOverride propertyOverride in overrides)
        {
            if (!propertyOverride.IsNamed) { continue; }

            string? problem = ApplyOne(item, propertyOverride, applied);
            if (problem is not null) { (problems ??= []).Add(problem); }
        }

        return problems ?? [];
    }

    private static string? ApplyOne(Item item, PropertyOverride propertyOverride, ICollection<AppliedOverride>? applied)
    {
        object? target = ResolveTarget(item, propertyOverride, out string? resolveError);
        if (target is null) { return resolveError; }

        if (target is not ISerializableEntity serializable
            || serializable.SerializableProperties is null
            || !serializable.SerializableProperties.TryGetValue(propertyOverride.PropertyName.ToIdentifier(), out SerializableProperty? property))
        {
            return $"Not found serializable property '{propertyOverride.PropertyName}' ({propertyOverride})";
        }

        object? before = property.GetValue(target);

        if (string.Equals(propertyOverride.Value, PropertyText.Format(before), StringComparison.Ordinal)
            || string.Equals(propertyOverride.Value, DeclaredOf(item, propertyOverride, property), StringComparison.Ordinal))
        {
            return null;
        }

        if (!property.TrySetValue(target, propertyOverride.Value)) { return $"Failed to set '{propertyOverride}'"; }

        if (!IsFinite(property.GetValue(target)))
        {
            property.SetValue(target, before);
            return $"'{propertyOverride}' would store a value that is not a number; kept the old one";
        }

        if (property.GetValue(target) is null && before is not null && GameMain.NetworkMember is not null)
        {
            property.SetValue(target, before);
            return $"'{propertyOverride}' would leave a value the network cannot carry; kept the old one";
        }

        applied?.Add(new AppliedOverride(serializable, property));
        return null;
    }

    private static bool IsFinite(object? value) => value switch
    {
        float number => float.IsFinite(number),
        double number => double.IsFinite(number),
        Vector2 vector => float.IsFinite(vector.X) && float.IsFinite(vector.Y),
        Vector3 vector => float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z),
        Vector4 vector => float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z) && float.IsFinite(vector.W),
        _ => true
    };

    private static string DeclaredOf(Item item, PropertyOverride propertyOverride, SerializableProperty property)
        => PropertyText.DeclaredValue(
            property,
            PropertyText.ElementOf(item.Prefab, propertyOverride.ComponentName, propertyOverride.ComponentIndex));

    private static object? ResolveTarget(Item item, PropertyOverride propertyOverride, out string? error)
    {
        error = null;

        if (string.IsNullOrEmpty(propertyOverride.ComponentName)) { return item; }

        if (ComponentOrdinal.Find(item.Components, propertyOverride.ComponentName, propertyOverride.ComponentIndex, static candidate => candidate.Name)
            is { } component)
        {
            return component;
        }

        error = $"Not found component '{propertyOverride.ComponentName}[{propertyOverride.ComponentIndex}]' of '{item.Prefab?.Identifier.Value}'";
        return null;
    }
}
