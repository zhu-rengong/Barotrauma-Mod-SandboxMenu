namespace SandboxMenu.Networking;

// Why a request did not turn into items. The status is what the client words for the player; the free-form
// problem lines that come with it are diagnostics and stay in the log.
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

// See SpawnRequest: declaration order is the wire order.
//
// Only the response to the newest request is shown, but the id travels anyway so a late answer to an older
// request cannot overwrite the status of the one the player just made.
public readonly record struct SpawnResponse(
    [property: NetworkSerialize] uint RequestId,
    [property: NetworkSerialize] SpawnStatus Status,
    [property: NetworkSerialize] int Queued,
    [property: NetworkSerialize(ArrayMaxSize = SpawnPayloadCodec.MaxProblems)] string[] Problems) : INetSerializableStruct;
