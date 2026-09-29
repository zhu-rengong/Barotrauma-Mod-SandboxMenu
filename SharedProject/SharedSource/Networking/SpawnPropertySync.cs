using Barotrauma.Networking;

namespace SandboxMenu.Networking;

internal static class SpawnPropertySync
{
    internal static void Publish(Item item, IReadOnlyList<AppliedOverride> applied, SpawnResult result)
    {
        if (applied.Count == 0) { return; }

        List<(object obj, SerializableProperty property)> editable = item.GetProperties<Editable>();

        if (editable.Count <= 1)
        {
            result.Report(StaysOnServer(item, $"the item has {editable.Count} editable propert(y/ies)"));
            return;
        }

        foreach (AppliedOverride propertyOverride in applied)
        {
            SerializableProperty property = propertyOverride.Property;

            if (!editable.Any(entry => entry.property == property && ReferenceEquals(entry.obj, propertyOverride.Target)))
            {
                result.Report(StaysOnServer(item, $"'{property.Name}' is not editable there"));
                continue;
            }

            if (!Travels(property.GetValue(propertyOverride.Target)))
            {
                result.Report(StaysOnServer(item, $"the value type '{property.PropertyType.Name}' cannot be written"));
                continue;
            }

            Plugin.NetworkService.CreateVanillaEntityEvent(item, new Item.ChangePropertyEventData(property, propertyOverride.Target));
        }
    }

    private static string StaysOnServer(Item item, string reason)
        => $"A property override of '{item.Prefab?.Identifier.Value}' stays on the server ({reason}).";

    private static bool Travels(object? value) => value switch
    {
        string or Identifier or float or int or bool or Color or Vector2 or Vector3 or Vector4 or Point or Rectangle
            or string[] => true,
        Enum => true,
        _ => false
    };
}
