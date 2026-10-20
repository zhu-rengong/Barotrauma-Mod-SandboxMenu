namespace SandboxMenu.Networking;

public enum AfflictionStatus
{
    Ok = 0,
    Denied = 1,
    UnknownAffliction = 2,
    NoCharacter = 3,
    Blocked = 4,
    Incompatible = 5,
    NoResponse = 6,
    BadPayload = 7
}

public readonly record struct AfflictionResponse(
    [property: NetworkSerialize] uint RequestId,
    [property: NetworkSerialize] byte Status) : INetSerializableStruct;
