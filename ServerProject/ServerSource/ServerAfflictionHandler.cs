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

        if (FindAffliction(request.AfflictionId) is not { } affliction)
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

    private static AfflictionPrefab? FindAffliction(uint id)
    {
        if (_resolved is { } cached && _resolvedId == id
            && AfflictionPrefab.Prefabs.TryGet(cached.Identifier, out AfflictionPrefab? active)
            && ReferenceEquals(active, cached))
        {
            return cached;
        }

        AfflictionPrefab? found = AfflictionPrefab.Prefabs.Find(prefab => prefab.UintIdentifier == id);

        _resolved = found;
        _resolvedId = id;

        return found;
    }

    private static bool Targets(CharacterHealth health, Affliction affliction, int[] limbs)
    {
        if (limbs.Length == 0) { return true; }

        if (health.GetAfflictionLimb(affliction) is not { } limb) { return false; }

        for (int i = 0; i < limbs.Length; i++)
        {
            if (limbs[i] == limb.HealthIndex) { return true; }
        }

        return false;
    }

    private static Limb? Limbs(Character character, int healthIndex)
    {
        if (character.AnimController is not { } controller) { return null; }

        Limb[] candidates = controller.Limbs;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].HealthIndex == healthIndex) { return candidates[i]; }
        }

        return null;
    }

    private static AfflictionPrefab? _resolved;
    private static uint _resolvedId;

    private static void Reply(Client client, uint requestId, AfflictionStatus status)
    {
        AfflictionResponse response = new(requestId, (byte)status);

        Services.GameNetwork.SendToClient(client, NetworkHeaders.AfflictionResponse, response);
    }
}
