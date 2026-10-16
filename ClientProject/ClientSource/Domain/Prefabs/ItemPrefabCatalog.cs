using System.Collections.Frozen;
using System.Collections.Immutable;

namespace SandboxMenu.Domain.Prefabs;

internal sealed record ItemPrefabEntry(ItemPrefab Prefab, string PackageKey, MapEntityCategory Category, string Tags)
{
    internal string Identifier => Prefab.Identifier.Value;

    internal bool Matches(string text)
        => Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Identifier.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Tags.Contains(text, StringComparison.OrdinalIgnoreCase);

    private string Name => Prefab.Name?.Value is { Length: > 0 } name ? name : Identifier;
}

internal sealed record ItemFilter(IReadOnlySet<string> Packages, MapEntityCategory Categories)
{
    internal bool Allows(ItemPrefabEntry entry)
        => (Packages.Count == 0 || (entry.PackageKey.Length > 0 && Packages.Contains(entry.PackageKey)))
            && (Categories == MapEntityCategory.None || (entry.Category & Categories) != 0);
}

internal static class ItemPrefabCatalog
{
    internal static readonly ImmutableArray<MapEntityCategory> AllCategories =
        [.. Enum.GetValues<MapEntityCategory>().Where(category => category != MapEntityCategory.None)];

    private static ImmutableArray<ItemPrefabEntry> _entries = ImmutableArray<ItemPrefabEntry>.Empty;
    private static IReadOnlyList<PackageLabel> _packages = [];
    private static MapEntityCategory _categoryMask = MapEntityCategory.None;
    private static FrozenDictionary<string, MapEntityCategory> _categoryMasksByPackage = FrozenDictionary<string, MapEntityCategory>.Empty;

    internal static IReadOnlyList<ItemPrefabEntry> All()
    {
        EnsureBuilt();

        return _entries;
    }

    internal static IReadOnlyList<PackageLabel> Packages()
    {
        EnsureBuilt();

        return _packages;
    }

    internal static void Reset()
    {
        _entries = ImmutableArray<ItemPrefabEntry>.Empty;
        _packages = [];
        _categoryMask = MapEntityCategory.None;
        _categoryMasksByPackage = FrozenDictionary<string, MapEntityCategory>.Empty;
    }

    internal static MapEntityCategory CategoryMaskIn(IReadOnlySet<string> packages)
    {
        EnsureBuilt();

        if (packages.Count == 0) { return _categoryMask; }

        MapEntityCategory available = MapEntityCategory.None;

        foreach (string package in packages)
        {
            if (_categoryMasksByPackage.TryGetValue(package, out MapEntityCategory mask)) { available |= mask; }
        }

        return available;
    }

    internal static IReadOnlyList<MapEntityCategory> CategoriesIn(IReadOnlySet<string> packages)
    {
        MapEntityCategory mask = CategoryMaskIn(packages);

        return [.. AllCategories.Where(category => (mask & category) != 0)];
    }

    private static ItemPrefabEntry Describe(ItemPrefab prefab)
    {
        string tags = prefab.Tags is null ? string.Empty : string.Join(' ', prefab.Tags.Select(tag => tag.Value));

        return new ItemPrefabEntry(prefab, PackageLabels.KeyOf(prefab.ContentPackage), prefab.Category, tags);
    }

    private static void EnsureBuilt()
    {
        if (!_entries.IsDefaultOrEmpty) { return; }

        List<ItemPrefabEntry> list = [];
        List<(ContentPackage Package, string Name)> found = [];
        HashSet<string> seen = new(PackageLabels.KeyComparer);
        Dictionary<string, MapEntityCategory> masks = new(PackageLabels.KeyComparer);
        MapEntityCategory categoryMask = MapEntityCategory.None;

        foreach (ItemPrefab prefab in ItemPrefab.Prefabs)
        {
            if (prefab is null || string.IsNullOrEmpty(prefab.Identifier.Value)) { continue; }

            ItemPrefabEntry entry = Describe(prefab);
            list.Add(entry);

            categoryMask |= entry.Category;

            if (entry.PackageKey.Length == 0) { continue; }

            if (prefab.ContentPackage is { } package && seen.Add(package.Path)) { found.Add((package, package.Name)); }

            masks[entry.PackageKey] = masks.TryGetValue(entry.PackageKey, out MapEntityCategory mask) ? mask | entry.Category : entry.Category;
        }

        list.Sort(static (a, b) => string.Compare(a.Identifier, b.Identifier, StringComparison.OrdinalIgnoreCase));

        _entries = list.ToImmutableArray();
        _packages = PackageLabels.Build(found);
        _categoryMask = categoryMask;
        _categoryMasksByPackage = masks.ToFrozenDictionary(PackageLabels.KeyComparer);
    }
}
