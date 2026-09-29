namespace SandboxMenu.Networking;

public enum SpawnTargetKind
{
    Inventory = 0,
    World = 1
}

public readonly record struct SpawnRequest(
    [property: NetworkSerialize(ArrayMaxSize = SpawnPayloadCodec.MaxCompressedBytes)] byte[] Payload,
    [property: NetworkSerialize] SpawnTargetKind Target,
    [property: NetworkSerialize] Vector2 Position,
    [property: NetworkSerialize] ushort CharacterId,
    [property: NetworkSerialize] uint RequestId) : INetSerializableStruct;
