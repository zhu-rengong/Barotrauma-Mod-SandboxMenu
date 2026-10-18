namespace SandboxMenu.Networking;

public enum AfflictionStatus
{
    Ok = 0,
    Denied = 1,
    UnknownAffliction = 2,
    NoCharacter = 3,
    Throttled = 4,
    Blocked = 5
}

public readonly record struct AfflictionResponse(
    [property: NetworkSerialize] uint RequestId,
    [property: NetworkSerialize] AfflictionStatus Status) : INetSerializableStruct;
