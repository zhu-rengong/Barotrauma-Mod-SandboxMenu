namespace SandboxMenu.Networking;

public enum SpawnTargetKind
{
    Inventory = 0,
    World = 1
}

// The target travels as a byte: a field of this assembly's enum type would have the host cache a closed generic
// over that type in NetSerializableProperties.TypeBehaviors, a table no unload ever purges.
public readonly record struct SpawnRequest(
    [property: NetworkSerialize(ArrayMaxSize = SpawnPayloadCodec.MaxCompressedBytes)] byte[] Payload,
    [property: NetworkSerialize] byte Target,
    [property: NetworkSerialize] Vector2 Position,
    [property: NetworkSerialize] ushort CharacterId,
    [property: NetworkSerialize] uint RequestId) : INetSerializableStruct;
