namespace SandboxMenu.Domain.Spawning;

internal static class SpawnPlanner
{
    internal static int RepeatCount(ValueRange? amount)
        => amount is { } range ? Math.Max(0, (int)MathF.Round(range.Roll(true))) : 1;

    internal static float ResolveAmount(ItemEntry entry, ItemPrefab prefab, Inventory? destination)
    {
        if (entry.Amount is { } amount) { return amount.Roll(entry.AmountRound); }

        if (entry.FillInventory && destination is not null)
        {
            return destination.HowManyCanBePut(prefab);
        }

        return 1f;
    }

    internal static float? FractionalCondition(float amount, int whole, float health)
        => amount < whole ? (amount - MathF.Floor(amount)) * health : null;

    internal static int StacksToItemCount(float stackCount, int perStack)
        => Math.Max(0, (int)MathF.Round(stackCount * perStack, MidpointRounding.AwayFromZero));

    internal static int StackCapacity(ItemPrefab prefab, Inventory destination)
    {
        int limit = Math.Max(1, prefab.GetMaxStackSize(destination));
        int free = destination.HowManyCanBePut(prefab);
        return Math.Max(1, Math.Min(limit, free + 1));
    }
}
