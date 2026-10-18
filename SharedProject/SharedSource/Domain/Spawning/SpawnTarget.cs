namespace SandboxMenu.Domain.Spawning;

internal abstract record SpawnTarget(Character? Owner)
{
    internal sealed record IntoInventory(Inventory Inventory, Character? Owner) : SpawnTarget(Owner);

    internal sealed record AtWorld(Vector2 WorldPosition, Character? Owner) : SpawnTarget(Owner);
}
