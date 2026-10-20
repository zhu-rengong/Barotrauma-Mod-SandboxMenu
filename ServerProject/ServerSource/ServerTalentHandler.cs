using Barotrauma.Networking;

namespace SandboxMenu;

internal static class ServerTalentHandler
{
    internal static void Handle(TalentRequest request, Client client)
        => ServerPolicy.Guard("Handling a talent request", () => Respond(request, client));

    private static void Respond(TalentRequest request, Client client)
    {
        if (client is null) { return; }

        if (!ServerPolicy.HasPermission(client))
        {
            Services.DebugConsole.NewMessage($"{client.Name} asked to grant a talent without the permission to do so.");
            Reply(client, request.RequestId, TalentStatus.Denied);
            return;
        }

        if (!ModCompatibility.Matches(request.ProtocolVersion, request.ModVersion, request.ModHash, client.Name, "talent"))
        {
            Reply(client, request.RequestId, TalentStatus.Incompatible);
            return;
        }

        if (TalentPrefab.TalentPrefabs.Find(prefab => prefab.UintIdentifier == request.TalentId) is not { } talent)
        {
            Reply(client, request.RequestId, TalentStatus.UnknownTalent);
            return;
        }

        if (Entity.FindEntityByID(request.CharacterId) is not Character character || character.Removed || character.Info is null)
        {
            Reply(client, request.RequestId, TalentStatus.NoCharacter);
            return;
        }

        character.GiveTalent(talent, true);

        Reply(client, request.RequestId, TalentStatus.Ok);
    }

    private static void Reply(Client client, uint requestId, TalentStatus status)
    {
        TalentResponse response = new(requestId, (byte)status);

        Services.GameNetwork.SendToClient(client, NetworkHeaders.TalentResponse, response);
    }
}
