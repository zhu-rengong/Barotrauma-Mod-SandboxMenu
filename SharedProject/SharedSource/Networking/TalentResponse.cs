namespace SandboxMenu.Networking;

public enum TalentStatus
{
    Ok = 0,
    Denied = 1,
    UnknownTalent = 2,
    NoCharacter = 3,
    Incompatible = 4,
    NoResponse = 5
}

public readonly record struct TalentResponse(
    [property: NetworkSerialize] uint RequestId,
    [property: NetworkSerialize] byte Status) : INetSerializableStruct;
