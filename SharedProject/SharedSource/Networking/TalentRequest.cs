namespace SandboxMenu.Networking;

public readonly record struct TalentRequest(
    [property: NetworkSerialize] uint TalentId,
    [property: NetworkSerialize] ushort ProtocolVersion,
    [property: NetworkSerialize] ushort CharacterId,
    [property: NetworkSerialize] uint RequestId,
    [property: NetworkSerialize] string ModVersion,
    [property: NetworkSerialize] string ModHash) : INetSerializableStruct;
