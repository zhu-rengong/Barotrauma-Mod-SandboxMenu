using System.Collections.Frozen;
using System.Collections.Immutable;

namespace SandboxMenu.Domain.Prefabs;

internal sealed class ContainerRules
{
    private static FrozenDictionary<string, ContainerRules?>? _cache;

    private readonly ImmutableArray<RelatedItem> _containable;

    private ContainerRules(ImmutableArray<RelatedItem> containable) => _containable = containable;

    internal static ContainerRules? For(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) { return null; }

        return Cache().TryGetValue(identifier, out ContainerRules? rules) ? rules : null;
    }

    internal static void Reset() => _cache = null;

    internal bool Allows(ItemPrefab prefab)
        => _containable.IsDefaultOrEmpty || _containable.Any(rule => rule.MatchesItem(prefab));

    private static FrozenDictionary<string, ContainerRules?> Cache()
    {
        if (_cache is { } cached) { return cached; }

        Dictionary<string, ContainerRules?> rules = new(PackageLabels.KeyComparer);

        foreach (ItemPrefabEntry entry in ItemPrefabCatalog.All())
        {
            rules.TryAdd(entry.Identifier, Build(entry.Prefab));
        }

        return _cache = rules.ToFrozenDictionary(PackageLabels.KeyComparer);
    }

    private static ContainerRules? Build(ItemPrefab prefab)
    {
        if (prefab.ConfigElement?.GetChildElement("ItemContainer") is not { } container) { return null; }

        ImmutableArray<RelatedItem>.Builder containable = ImmutableArray.CreateBuilder<RelatedItem>();

        Collect(container, prefab.Identifier.Value, containable);

        return new ContainerRules(containable.ToImmutable());
    }

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
