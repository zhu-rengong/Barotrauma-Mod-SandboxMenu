using Barotrauma.Networking;

namespace SandboxMenu.Domain.Spawning;

internal static class VanillaPropertySync
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

            if (!Editable(editable, propertyOverride))
            {
                result.Report(StaysOnServer(item, $"'{property.Name}' is not editable there"));
                continue;
            }

            if (!PropertyText.Travels(property.GetValue(propertyOverride.Target)))
            {
                result.Report(StaysOnServer(item, $"the value type '{property.PropertyType.Name}' cannot be written"));
                continue;
            }

            Services.GameNetwork.CreateVanillaEntityEvent(item, new Item.ChangePropertyEventData(property, propertyOverride.Target));
        }
    }

    private static bool Editable(List<(object obj, SerializableProperty property)> editable, AppliedOverride applied)
    {
        for (int i = 0; i < editable.Count; i++)
        {
            if (editable[i].property == applied.Property && ReferenceEquals(editable[i].obj, applied.Target)) { return true; }
        }

        return false;
    }

    private static string StaysOnServer(Item item, string reason)
        => $"A property override of '{item.Prefab?.Identifier.Value}' stays on the server ({reason}).";
}
