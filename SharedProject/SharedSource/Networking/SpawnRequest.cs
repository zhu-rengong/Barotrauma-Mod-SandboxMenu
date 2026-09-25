namespace SandboxMenu.Networking;

// Which of the two spawn targets the request asks for. The server is the one that decides whether the request is
// allowed at all, so this only says what the player pressed.
public enum SpawnTargetKind
{
    Inventory = 0,
    World = 1
}

// The members are the wire format: the game serialises them in the order of the line each attribute sits on, and
// both halves share this file, so the declaration order below is the protocol.
//
// Position only means anything for SpawnTargetKind.World, and CharacterId is what the client believes it is
// controlling — the server acts on its own idea of the client's character and only uses this to spot a mismatch.
public readonly record struct SpawnRequest(
    [property: NetworkSerialize(ArrayMaxSize = SpawnPayloadCodec.MaxCompressedBytes)] byte[] Payload,
    [property: NetworkSerialize] SpawnTargetKind Target,
    [property: NetworkSerialize] Vector2 Position,
    [property: NetworkSerialize] ushort CharacterId,
    [property: NetworkSerialize] uint RequestId) : INetSerializableStruct;
