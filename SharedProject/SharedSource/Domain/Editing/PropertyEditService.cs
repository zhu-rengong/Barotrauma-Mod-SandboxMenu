using Barotrauma.Items.Components;

namespace SandboxMenu.Domain.Editing;

// An override that really landed: the entity it was written to and the property it went through. Whoever reports it
// to the clients needs both — a property alone does not say which component of the item carries it.
internal readonly record struct AppliedOverride(ISerializableEntity Target, SerializableProperty Property);

internal static class PropertyEditService
{
    // applied is filled with what was written, for the caller that has to pass it on; the callers that only care
    // about what failed leave it out.
    internal static IReadOnlyList<string> Apply(
        Item item,
        IReadOnlyList<PropertyOverride> overrides,
        ICollection<AppliedOverride>? applied = null)
    {
        if (overrides.Count == 0) { return []; }

        List<string>? problems = null;

        foreach (PropertyOverride over in overrides)
        {
            if (string.IsNullOrEmpty(over.PropertyName)) { continue; }

            string? problem = ApplyOne(item, over, applied);
            if (problem is not null) { (problems ??= []).Add(problem); }
        }

        return problems ?? [];
    }

    internal static bool SetComponentValue(Item item, string componentName, int componentIndex, string propertyName, string value)
        => Apply(item,
        [
            new PropertyOverride
            {
                ComponentName = componentName,
                ComponentIndex = componentIndex,
                PropertyName = propertyName,
                Value = value
            }
        ]).Count == 0;

    private static string? ApplyOne(Item item, PropertyOverride over, ICollection<AppliedOverride>? applied)
    {
        object? target = ResolveTarget(item, over, out string? resolveError);
        if (target is null) { return resolveError; }

        if (target is not ISerializableEntity serializable
            || serializable.SerializableProperties is null
            || !serializable.SerializableProperties.TryGetValue(Identifiers.Of(over.PropertyName), out SerializableProperty? property))
        {
            return $"Not found serializable property '{over.PropertyName}' ({over})";
        }

        if (!property.TrySetValue(target, over.Value)) { return $"Failed to set '{over}'"; }

        applied?.Add(new AppliedOverride(serializable, property));
        return null;
    }

    private static object? ResolveTarget(Item item, PropertyOverride over, out string? error)
    {
        error = null;

        if (string.IsNullOrEmpty(over.ComponentName)) { return item; }

        int wanted = Math.Max(1, over.ComponentIndex);
        int index = 0;

        foreach (ItemComponent component in item.Components)
        {
            if (!string.Equals(component.Name, over.ComponentName, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (++index == wanted) { return component; }
        }

        error = $"Not found component '{over.ComponentName}[{over.ComponentIndex}]' of '{item.Prefab?.Identifier.Value}'";
        return null;
    }
}
