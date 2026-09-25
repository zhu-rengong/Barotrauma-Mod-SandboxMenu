namespace SandboxMenu.Domain.Spawning;

internal abstract record SpawnTarget
{
    internal sealed record IntoInventory(Inventory Inventory, Character? Character) : SpawnTarget;

    internal sealed record AtWorld(Vector2 WorldPosition, Character? Character) : SpawnTarget;

    internal Character? Owner => this switch
    {
        IntoInventory into => into.Character,
        AtWorld world => world.Character,
        _ => null
    };
}
