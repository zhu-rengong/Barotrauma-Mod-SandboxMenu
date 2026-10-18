namespace SandboxMenu.Networking;

public enum NetworkHeaders : byte
{
    SpawnRequest = 0,
    SpawnResponse = 1,
    AfflictionRequest = 2,
    AfflictionResponse = 3,
    TalentRequest = 4,
    TalentResponse = 5
}

internal static class Protocol
{
    internal const ushort Version = 3;
}
