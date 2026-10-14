using Barotrauma.Networking;
using SandboxMenu.Domain.Afflictions;

namespace SandboxMenu;

internal static class ServerAfflictionHandler
{
    private const string CooldownField = $"{ModInfo.Prefix}.afflictioncooldown";

    internal static void Handle(AfflictionRequest request, Client client)
        => ServerPolicy.Guard("Handling an affliction request", () => Respond(request, client));

    private static void Respond(AfflictionRequest request, Client client)
    {
        if (client is null) { return; }

        if (!ServerPolicy.IsAllowed(client))
        {
            DebugConsole.NewMessage($"{client.Name} asked to apply an affliction without the permission to do so.");
            Reply(client, request.RequestId, AfflictionStatus.Denied);
            return;
        }

        if (ServerPolicy.IsThrottled(client, CooldownField))
        {
            Reply(client, request.RequestId, AfflictionStatus.Throttled);
            return;
        }

        if (AfflictionPrefab.Prefabs.Find(prefab => prefab.UintIdentifier == request.AfflictionId) is not { } affliction)
        {
            Reply(client, request.RequestId, AfflictionStatus.UnknownAffliction);
            return;
        }

        if (Entity.FindEntityByID(request.CharacterId) is not Character character || character.Removed)
        {
            Reply(client, request.RequestId, AfflictionStatus.NoCharacter);
            return;
        }

        int[] limbs = request.Limbs ?? [];

        if ((AfflictionMode)request.Mode == AfflictionMode.Remove)
        {
            AfflictionEditor.Clear(character.CharacterHealth, afflictionInUse
                => afflictionInUse.Prefab == affliction && Targets(character.CharacterHealth, afflictionInUse, limbs));

            Reply(client, request.RequestId, AfflictionStatus.Ok);
            return;
        }

        float strength = Math.Clamp(request.Strength, 0f, affliction.MaxStrength > 0f ? affliction.MaxStrength : 1f);

        if (limbs.Length == 0)
        {
            character.CharacterHealth.ApplyAffliction(null, affliction.Instantiate(strength, client.Character));

            Reply(client, request.RequestId, AfflictionStatus.Ok);
            return;
        }

        foreach (int index in limbs)
        {
            if (Limbs(character, index) is not { } limb) { continue; }

            character.CharacterHealth.ApplyAffliction(limb, affliction.Instantiate(strength, client.Character));
        }

        Reply(client, request.RequestId, AfflictionStatus.Ok);
    }

    private static bool Targets(CharacterHealth health, Affliction affliction, int[] limbs)
        => limbs.Length == 0 || limbs.Contains(health.GetAfflictionLimb(affliction)?.HealthIndex ?? -1);

    private static Limb? Limbs(Character character, int healthIndex)
        => character.AnimController?.Limbs.FirstOrDefault(candidate => candidate.HealthIndex == healthIndex);

    private static void Reply(Client client, uint requestId, AfflictionStatus status)
    {
        AfflictionResponse response = new(requestId, (byte)status);

        Services.GameNetwork.SendToClient(client, NetworkHeaders.AfflictionResponse, response);
    }
}
