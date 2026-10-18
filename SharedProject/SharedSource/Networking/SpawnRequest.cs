namespace SandboxMenu.Networking;

public enum SpawnTargetKind
{
    Inventory = 0,
    World = 1
}

public readonly record struct SpawnRequest(
    [property: NetworkSerialize(ArrayMaxSize = SpawnMessageCodec.MaxCompressedBytes)] byte[] Payload,
    [property: NetworkSerialize] ushort ProtocolVersion,
    [property: NetworkSerialize] byte Target,
    [property: NetworkSerialize] Vector2 Position,
    [property: NetworkSerialize] ushort CharacterId,
    [property: NetworkSerialize] uint RequestId) : INetSerializableStruct;
