using System.Collections.Immutable;

namespace SandboxMenu.Domain.Prefabs;

// What an entry can hold is decided by the <containable> entries (RelatedItem) of its first ItemContainer - the same
// one that takes the nested items when the set is spawned - and a container without any containable takes anything.
// Null means the item has no container, so there is nothing to filter by. The matching itself is the host's.
internal sealed class ContainerRules
{
    private readonly ImmutableArray<RelatedItem> _containable;

    private ContainerRules(ImmutableArray<RelatedItem> containable) => _containable = containable;

    internal static ContainerRules? For(string? identifier)
    {
        if (ItemPrefabLookup.By(identifier)?.ConfigElement?.GetChildElement("ItemContainer") is not { } container)
        {
            return null;
        }

        ImmutableArray<RelatedItem>.Builder containable = ImmutableArray.CreateBuilder<RelatedItem>();

        Collect(container, identifier ?? string.Empty, containable);

        return new ContainerRules(containable.ToImmutable());
    }

    internal bool Allows(ItemPrefab prefab)
        => _containable.IsDefaultOrEmpty || _containable.Any(rule => rule.MatchesItem(prefab));

    private static void Collect(ContentXElement container, string parentName, ImmutableArray<RelatedItem>.Builder containable)
    {
        foreach (ContentXElement element in container.Elements())
        {
            string name = element.Name.LocalName;

            if (string.Equals(name, "containable", StringComparison.OrdinalIgnoreCase))
            {
                if (RelatedItem.Load(element, false, parentName) is { } rule) { containable.Add(rule); }

                continue;
            }

            if (string.Equals(name, "subcontainer", StringComparison.OrdinalIgnoreCase)) { Collect(element, parentName, containable); }
        }
    }
}
