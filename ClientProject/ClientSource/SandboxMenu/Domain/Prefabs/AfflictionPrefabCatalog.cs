using System.Collections.Immutable;

namespace SandboxMenu.Domain.Prefabs;

internal sealed record AfflictionPrefabEntry(AfflictionPrefab Prefab, ContentPackage? Package, Identifier Type, bool IsBuff, bool LimbSpecific)
{
    internal string Identifier => Prefab.Identifier.Value;

    internal string Name => Prefab.Name?.Value is { Length: > 0 } name ? name : Identifier;

    internal string TypeValue => Type.Value;

    internal bool Matches(string text)
        => Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Identifier.Contains(text, StringComparison.OrdinalIgnoreCase)
            || TypeValue.Contains(text, StringComparison.OrdinalIgnoreCase);

    internal bool Hidden
        => Prefab.MaxStrength < Prefab.ShowIconThreshold
            && Prefab.MaxStrength < Prefab.ShowIconToOthersThreshold
            && Prefab.MaxStrength < Prefab.ShowInHealthScannerThreshold;
}

internal enum AfflictionScope
{
    Limb,
    Body
}

internal enum AfflictionKind
{
    Buff,
    Debuff
}

internal sealed record AfflictionFilter(
    IReadOnlySet<ContentPackage> Packages,
    IReadOnlySet<Identifier> Types,
    IReadOnlySet<AfflictionScope> Scopes,
    IReadOnlySet<AfflictionKind> Kinds)
{
    internal bool Allows(AfflictionPrefabEntry entry)
        => (Packages.Count == 0 || (entry.Package is { } package && Packages.Contains(package)))
            && (Types.Count == 0 || Types.Contains(entry.Type))
            && Matches(Scopes, entry.LimbSpecific ? AfflictionScope.Limb : AfflictionScope.Body)
            && Matches(Kinds, entry.IsBuff ? AfflictionKind.Buff : AfflictionKind.Debuff);

    private static bool Matches<T>(IReadOnlySet<T> selected, T value)
        => selected.Count == 0 || selected.Contains(value);
}

internal sealed record AfflictionPackage(ContentPackage Package, string Label);

internal static class AfflictionPrefabCatalog
{
    private static ImmutableArray<AfflictionPrefabEntry> _entries = ImmutableArray<AfflictionPrefabEntry>.Empty;
    private static IReadOnlyList<AfflictionPackage> _packages = [];
    private static IReadOnlyList<Identifier> _types = [];

    internal static IReadOnlyList<AfflictionPrefabEntry> All()
    {
        EnsureBuilt();

        return _entries;
    }

    internal static IReadOnlyList<AfflictionPackage> Packages()
    {
        EnsureBuilt();

        return _packages;
    }

    internal static IReadOnlyList<Identifier> Types()
    {
        EnsureBuilt();

        return _types;
    }

    internal static IReadOnlyList<Identifier> TypesIn(IReadOnlySet<ContentPackage> packages)
    {
        EnsureBuilt();

        if (packages.Count == 0) { return _types; }

        HashSet<Identifier> available = [];

        foreach (AfflictionPrefabEntry entry in _entries)
        {
            if (entry.Package is { } package && packages.Contains(package)) { available.Add(entry.Type); }
        }

        return [.. _types.Where(available.Contains)];
    }

    internal static void Reset()
    {
        _entries = ImmutableArray<AfflictionPrefabEntry>.Empty;
        _packages = [];
        _types = [];
    }

    private static void EnsureBuilt()
    {
        if (!_entries.IsDefaultOrEmpty) { return; }

        List<AfflictionPrefabEntry> list = [];
        List<(ContentPackage Package, string Name)> found = [];
        HashSet<ContentPackage> seen = new(ReferenceEqualityComparer.Instance);
        HashSet<Identifier> types = [];

        foreach (AfflictionPrefab prefab in AfflictionPrefab.Prefabs)
        {
            if (prefab is null || prefab.Identifier.IsEmpty) { continue; }

            list.Add(new AfflictionPrefabEntry(prefab, prefab.ContentPackage, prefab.AfflictionType, prefab.IsBuff, prefab.LimbSpecific));
            types.Add(prefab.AfflictionType);

            if (prefab.ContentPackage is not { } package) { continue; }

            if (package.Path.Length > 0 && seen.Add(package)) { found.Add((package, package.Name)); }
        }

        list.Sort(static (a, b) =>
        {
            int order = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

            return order != 0 ? order : string.Compare(a.Identifier, b.Identifier, StringComparison.OrdinalIgnoreCase);
        });

        _entries = [.. list];
        _packages = BuildPackages(found);
        _types = [.. types.OrderBy(type => type.Value, StringComparer.OrdinalIgnoreCase)];
    }

    private static IReadOnlyList<AfflictionPackage> BuildPackages(List<(ContentPackage Package, string Name)> found)
    {
        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);

        foreach ((ContentPackage _, string name) in found) { counts[name] = counts.GetValueOrDefault(name) + 1; }

        List<AfflictionPackage> packages = new(found.Count);

        foreach ((ContentPackage package, string name) in found)
        {
            string folder = FolderOf(package.Path);

            string label = name.Length == 0
                ? folder
                : counts[name] > 1 ? $"{name} ({folder})" : name;

            packages.Add(new AfflictionPackage(package, label));
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

internal static class AfflictionPrefabLookup
{
    internal static AfflictionPrefab? By(string? identifier)
        => !string.IsNullOrWhiteSpace(identifier) && AfflictionPrefab.Prefabs.TryGet(identifier, out AfflictionPrefab? prefab)
            ? prefab
            : null;
}
