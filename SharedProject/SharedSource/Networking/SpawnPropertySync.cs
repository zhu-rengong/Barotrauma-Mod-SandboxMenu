using Barotrauma.Networking;

namespace SandboxMenu.Networking;

// Telling the other clients about the component values a spawn wrote. This rides the host's own property event —
// Item.ChangePropertyEventData through IGameNetwork.CreateVanillaEntityEvent — so the clients apply the values with
// the very code the game uses for an in-game edit. The mod therefore has no wire format, no read handler and no
// second guess about what an override means on the far side; it only feeds the host what to write.
//
// What the host's event cannot carry, and what this reports as a problem instead of leaving as a silent difference
// between the server's item and everyone else's:
//   · a property the item does not expose as [Editable] — that is the set the host's write pass picks from when the
//     server is the writer (Item.WritePropertyChange is called with inGameEditableOnly: false from ServerEventWrite,
//     and the client reads with the same flag), and ChangePropertyEventData itself complains about anything outside it;
//   · a value of a type the host's writer has no case for: it ends in a NotImplementedException thrown while the
//     event is being written, which happens in the send pass, long after this frame is gone and out of reach of any
//     guard of ours;
//   · an item whose editable properties number one, where the writer leaves out the property name it writes in every
//     other case while the reader reads one regardless.
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

        foreach (AppliedOverride over in applied)
        {
            if (!editable.Any(p => p.property == over.Property && ReferenceEquals(p.obj, over.Target)))
            {
                result.Report(StaysOnServer(item, $"'{over.Property.Name}' is not editable there"));
                continue;
            }

            if (!Travels(over.Property.GetValue(over.Target)))
            {
                result.Report(StaysOnServer(item, $"the value type '{over.Property.PropertyType.Name}' cannot be written"));
                continue;
            }

            Plugin.NetworkService.CreateVanillaEntityEvent(item, new Item.ChangePropertyEventData(over.Property, over.Target));
        }
    }

    private static string StaysOnServer(Item item, string reason)
        => $"A property override of '{item.Prefab?.Identifier.Value}' stays on the server ({reason}).";

    // The host's value switch, one for one: these are the types Item.WritePropertyChange has a case for.
    private static bool Travels(object? value) => value switch
    {
        string or Identifier or float or int or bool or Color or Vector2 or Vector3 or Vector4 or Point or Rectangle
            or string[] => true,
        Enum => true,
        _ => false
    };
}
