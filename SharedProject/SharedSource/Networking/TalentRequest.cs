namespace SandboxMenu.Networking;

public readonly record struct TalentRequest(
    [property: NetworkSerialize] uint TalentId,
    [property: NetworkSerialize] ushort ProtocolVersion,
    [property: NetworkSerialize] ushort CharacterId,
    [property: NetworkSerialize] uint RequestId) : INetSerializableStruct;
