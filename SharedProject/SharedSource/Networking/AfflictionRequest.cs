namespace SandboxMenu.Networking;

public enum AfflictionMode
{
    Apply = 0,
    Remove = 1
}

public readonly record struct AfflictionRequest(
    [property: NetworkSerialize] uint AfflictionId,
    [property: NetworkSerialize] float Strength,
    [property: NetworkSerialize(ArrayMaxSize = 24)] int[] Limbs,
    [property: NetworkSerialize] ushort ProtocolVersion,
    [property: NetworkSerialize] byte Mode,
    [property: NetworkSerialize] ushort CharacterId,
    [property: NetworkSerialize] uint RequestId,
    [property: NetworkSerialize] bool MultiplyByMaxVitality) : INetSerializableStruct;
