namespace SandboxMenu.Networking;

public enum SpawnStatus
{
    Ok = 0,
    Denied = 1,
    BadPayload = 2,
    NoCharacter = 3,
    NoSpawner = 4,
    TooLarge = 5,
    Throttled = 6
}

public readonly record struct SpawnResponse(
    [property: NetworkSerialize] uint RequestId,
    [property: NetworkSerialize] byte Status,
    [property: NetworkSerialize] int Queued,
    [property: NetworkSerialize(ArrayMaxSize = SpawnPayloadCodec.MaxProblems)] string[] Problems) : INetSerializableStruct;
