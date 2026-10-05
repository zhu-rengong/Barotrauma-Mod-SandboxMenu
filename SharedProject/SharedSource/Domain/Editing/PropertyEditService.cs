using Barotrauma.Items.Components;

namespace SandboxMenu.Domain.Editing;

internal readonly record struct AppliedOverride(ISerializableEntity Target, SerializableProperty Property);

internal static class PropertyEditService
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

    private static string? ApplyOne(Item item, PropertyOverride propertyOverride, ICollection<AppliedOverride>? applied)
    {
        object? target = ResolveTarget(item, propertyOverride, out string? resolveError);
        if (target is null) { return resolveError; }

        if (target is not ISerializableEntity serializable
            || serializable.SerializableProperties is null
            || !serializable.SerializableProperties.TryGetValue(Identifiers.Of(propertyOverride.PropertyName), out SerializableProperty? property))
        {
            return $"Not found serializable property '{propertyOverride.PropertyName}' ({propertyOverride})";
        }

        object? before = property.GetValue(target);

        // A write that cannot change anything is left out. That is not only tidiness: some setters turn an empty
        // string into null (Item.DescriptionTag does), and the host's change-property network event cannot carry a
        // null — the server throws while writing it and the clients are dropped for the desync that follows. A value
        // the property already holds, or the one it holds by declaration, never needs to go anywhere.
        if (string.Equals(propertyOverride.Value, PropertyDefaults.Format(before), StringComparison.Ordinal)
            || string.Equals(propertyOverride.Value, DeclaredOf(item, propertyOverride, property), StringComparison.Ordinal))
        {
            return null;
        }

        if (!property.TrySetValue(target, propertyOverride.Value)) { return $"Failed to set '{propertyOverride}'"; }

        // What the write left behind has to be something the network can carry. The event the setter queues reads the
        // property when it goes out rather than now, so putting the old value back here means it leaves carrying that
        // instead of failing on the server.
        if (property.GetValue(target) is null && before is not null && GameMain.NetworkMember is not null)
        {
            property.SetValue(target, before);
            return $"'{propertyOverride}' would leave a value the network cannot carry; kept the old one";
        }

        applied?.Add(new AppliedOverride(serializable, property));
        return null;
    }

    // What the property carries by declaration: the same value the property picker shows behind its label, so a row
    // reading "= 50,11" and a write of 50,11 agree on what changes nothing.
    private static string DeclaredOf(Item item, PropertyOverride propertyOverride, SerializableProperty property)
        => PropertyDefaults.DeclaredValue(
            property,
            PropertyDefaults.ElementOf(item.Prefab, propertyOverride.ComponentName, propertyOverride.ComponentIndex));

    private static object? ResolveTarget(Item item, PropertyOverride propertyOverride, out string? error)
    {
        error = null;

        if (string.IsNullOrEmpty(propertyOverride.ComponentName)) { return item; }

        int wantedIndex = Math.Max(1, propertyOverride.ComponentIndex);
        int seen = 0;

        foreach (ItemComponent component in item.Components)
        {
            if (!string.Equals(component.Name, propertyOverride.ComponentName, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (++seen == wantedIndex) { return component; }
        }

        error = $"Not found component '{propertyOverride.ComponentName}[{propertyOverride.ComponentIndex}]' of '{item.Prefab?.Identifier.Value}'";
        return null;
    }
}
