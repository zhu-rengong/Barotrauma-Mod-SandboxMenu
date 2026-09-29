using System.Collections.Immutable;

namespace SandboxMenu.Domain.Prefabs;

internal sealed record ItemPrefabEntry(ItemDisplay Display, string Package, MapEntityCategory Category, string Tags)
{
    internal bool Matches(string text)
        => Display.Name.Value.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Display.Identifier.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Tags.Contains(text, StringComparison.OrdinalIgnoreCase);
}

internal sealed record ItemFilter(IReadOnlySet<string> Packages, MapEntityCategory Categories)
{
    internal bool Allows(ItemPrefabEntry entry)
        => (Packages.Count == 0 || Packages.Contains(entry.Package))
            && (Categories == MapEntityCategory.None || (entry.Category & Categories) != 0);
}

internal static class ItemPrefabCatalog
{
    internal static readonly ImmutableArray<MapEntityCategory> AllCategories =
        [.. Enum.GetValues<MapEntityCategory>().Where(category => category != MapEntityCategory.None)];

    private static ImmutableArray<ItemPrefabEntry> _entries = ImmutableArray<ItemPrefabEntry>.Empty;
    private static IReadOnlyList<string>? _packages;
    private static IReadOnlyList<MapEntityCategory>? _categories;

    static ItemPrefabCatalog() => StaticState.Register(Invalidate);

    internal static void Invalidate()
    {
        _entries = ImmutableArray<ItemPrefabEntry>.Empty;
        _packages = null;
        _categories = null;
    }

    internal static IReadOnlyList<ItemPrefabEntry> All()
    {
        EnsureBuilt();

        return _entries;
    }

    internal static IReadOnlyList<string> Packages()
    {
        EnsureBuilt();

        if (_entries.IsDefaultOrEmpty) { return []; }

        return _packages ??= BuildPackages();
    }

    private static IReadOnlyList<string> BuildPackages()
        =>
        [
            .. _entries.Select(entry => entry.Package)
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        ];

    internal static IReadOnlyList<MapEntityCategory> Categories()
    {
        EnsureBuilt();

        if (_entries.IsDefaultOrEmpty) { return []; }

        return _categories ??= BuildCategories();
    }

    private static IReadOnlyList<MapEntityCategory> BuildCategories()
    {
        List<MapEntityCategory> categories = [];

        foreach (MapEntityCategory category in AllCategories)
        {
            if (_entries.Any(entry => (entry.Category & category) != 0)) { categories.Add(category); }
        }

        return categories;
    }

    private static ItemPrefabEntry Describe(ItemPrefab prefab)
    {
        ItemDisplay display = ItemDisplay.For(prefab);

        string tags = prefab.Tags is null ? string.Empty : string.Join(' ', prefab.Tags.Select(tag => tag.Value));

        return new ItemPrefabEntry(display, prefab.ContentPackage?.Name ?? string.Empty, prefab.Category, tags);
    }

    private static void EnsureBuilt()
    {
        if (!_entries.IsDefaultOrEmpty) { return; }

        List<ItemPrefabEntry> list = [];

        foreach (ItemPrefab prefab in ItemPrefab.Prefabs)
        {
            if (prefab is null || string.IsNullOrEmpty(prefab.Identifier.Value)) { continue; }

            list.Add(Describe(prefab));
        }

        list.Sort(static (a, b) => string.Compare(a.Display.Identifier, b.Display.Identifier, StringComparison.OrdinalIgnoreCase));

        _entries = list.ToImmutableArray();
    }
}
