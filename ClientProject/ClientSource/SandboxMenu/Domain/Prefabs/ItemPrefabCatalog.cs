using System.Collections.Frozen;
using System.Collections.Immutable;

namespace SandboxMenu.Domain.Prefabs;

internal sealed record ItemPrefabEntry(ItemPrefab Prefab, ContentPackage? Package, MapEntityCategory Category, string Tags)
{
    internal string Identifier => Prefab.Identifier.Value;

    internal bool Matches(string text)
        => Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Identifier.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Tags.Contains(text, StringComparison.OrdinalIgnoreCase);

    private string Name => Prefab.Name?.Value is { Length: > 0 } name ? name : Identifier;
}

internal sealed record ItemFilter(IReadOnlySet<ContentPackage> Packages, MapEntityCategory Categories)
{
    internal bool Allows(ItemPrefabEntry entry)
        => (Packages.Count == 0 || (entry.Package is { } package && Packages.Contains(package)))
            && (Categories == MapEntityCategory.None || (entry.Category & Categories) != 0);
}

internal sealed record ItemPackage(ContentPackage Package, string Label);

internal static class ItemPrefabCatalog
{
    internal static readonly ImmutableArray<MapEntityCategory> AllCategories =
        [.. Enum.GetValues<MapEntityCategory>().Where(category => category != MapEntityCategory.None)];

    private static ImmutableArray<ItemPrefabEntry> _entries = ImmutableArray<ItemPrefabEntry>.Empty;
    private static IReadOnlyList<ItemPackage> _packages = [];
    private static MapEntityCategory _categoryMask = MapEntityCategory.None;
    private static FrozenDictionary<ContentPackage, MapEntityCategory> _categoryMasksByPackage = FrozenDictionary<ContentPackage, MapEntityCategory>.Empty;

    internal static IReadOnlyList<ItemPrefabEntry> All()
    {
        EnsureBuilt();

        return _entries;
    }

    internal static IReadOnlyList<ItemPackage> Packages()
    {
        EnsureBuilt();

        return _packages;
    }

    internal static MapEntityCategory CategoryMaskIn(IReadOnlySet<ContentPackage> packages)
    {
        EnsureBuilt();

        if (packages.Count == 0) { return _categoryMask; }

        MapEntityCategory available = MapEntityCategory.None;

        foreach (ContentPackage package in packages)
        {
            if (_categoryMasksByPackage.TryGetValue(package, out MapEntityCategory mask)) { available |= mask; }
        }

        return available;
    }

    internal static IReadOnlyList<MapEntityCategory> CategoriesIn(IReadOnlySet<ContentPackage> packages)
        => [.. AllCategories.Where(category => (CategoryMaskIn(packages) & category) != 0)];

    private static ItemPrefabEntry Describe(ItemPrefab prefab)
    {
        string tags = prefab.Tags is null ? string.Empty : string.Join(' ', prefab.Tags.Select(tag => tag.Value));

        return new ItemPrefabEntry(prefab, prefab.ContentPackage, prefab.Category, tags);
    }

    private static void EnsureBuilt()
    {
        if (!_entries.IsDefaultOrEmpty) { return; }

        List<ItemPrefabEntry> list = [];
        List<(ContentPackage Package, string Name)> found = [];
        HashSet<ContentPackage> seen = new(ReferenceEqualityComparer.Instance);
        Dictionary<ContentPackage, MapEntityCategory> masks = new(ReferenceEqualityComparer.Instance);
        MapEntityCategory categoryMask = MapEntityCategory.None;

        foreach (ItemPrefab prefab in ItemPrefab.Prefabs)
        {
            if (prefab is null || string.IsNullOrEmpty(prefab.Identifier.Value)) { continue; }

            ItemPrefabEntry entry = Describe(prefab);
            list.Add(entry);

            categoryMask |= entry.Category;

            if (entry.Package is not { } package) { continue; }

            if (package.Path.Length > 0 && seen.Add(package)) { found.Add((package, package.Name)); }

            masks[package] = masks.TryGetValue(package, out MapEntityCategory mask) ? mask | entry.Category : entry.Category;
        }

        list.Sort(static (a, b) => string.Compare(a.Identifier, b.Identifier, StringComparison.OrdinalIgnoreCase));

        _entries = list.ToImmutableArray();
        _packages = BuildPackages(found);
        _categoryMask = categoryMask;
        _categoryMasksByPackage = masks.ToFrozenDictionary(ReferenceEqualityComparer.Instance);
    }

    private static IReadOnlyList<ItemPackage> BuildPackages(List<(ContentPackage Package, string Name)> found)
    {
        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);

        foreach ((ContentPackage _, string name) in found) { counts[name] = counts.GetValueOrDefault(name) + 1; }

        List<ItemPackage> packages = new(found.Count);

        foreach ((ContentPackage package, string name) in found)
        {
            string folder = FolderOf(package.Path);

            string label = name.Length == 0
                ? folder
                : counts[name] > 1 ? $"{name} ({folder})" : name;

            packages.Add(new ItemPackage(package, label));
        }

        packages.Sort(static (a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

        return packages;
    }

    private static string FolderOf(string path)
    {
        string folder = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);

        return folder.Length == 0 ? path : folder;
    }
}
