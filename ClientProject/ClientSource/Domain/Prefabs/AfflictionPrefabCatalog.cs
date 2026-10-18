using System.Collections.Immutable;

namespace SandboxMenu.Domain.Prefabs;

internal sealed record AfflictionPrefabEntry(AfflictionPrefab Prefab, string PackageKey, Identifier Type, bool IsBuff, bool LimbSpecific)
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
    IReadOnlySet<string> Packages,
    IReadOnlySet<Identifier> Types,
    IReadOnlySet<AfflictionScope> Scopes,
    IReadOnlySet<AfflictionKind> Kinds)
{
    internal bool Allows(AfflictionPrefabEntry entry)
        => (Packages.Count == 0 || (entry.PackageKey.Length > 0 && Packages.Contains(entry.PackageKey)))
            && (Types.Count == 0 || Types.Contains(entry.Type))
            && Matches(Scopes, entry.LimbSpecific ? AfflictionScope.Limb : AfflictionScope.Body)
            && Matches(Kinds, entry.IsBuff ? AfflictionKind.Buff : AfflictionKind.Debuff);

    private static bool Matches<T>(IReadOnlySet<T> selected, T value)
        => selected.Count == 0 || selected.Contains(value);
}

internal static class AfflictionPrefabCatalog
{
    private static ImmutableArray<AfflictionPrefabEntry> _entries = ImmutableArray<AfflictionPrefabEntry>.Empty;
    private static IReadOnlyList<PackageLabel> _packages = [];
    private static IReadOnlyList<Identifier> _types = [];

    internal static IReadOnlyList<AfflictionPrefabEntry> All()
    {
        EnsureBuilt();

        return _entries;
    }

    internal static IReadOnlyList<PackageLabel> Packages()
    {
        EnsureBuilt();

        return _packages;
    }

    internal static IReadOnlyList<Identifier> TypesIn(IReadOnlySet<string> packages)
    {
        EnsureBuilt();

        if (packages.Count == 0) { return _types; }

        HashSet<Identifier> available = [];

        foreach (AfflictionPrefabEntry entry in _entries)
        {
            if (entry.PackageKey.Length > 0 && packages.Contains(entry.PackageKey)) { available.Add(entry.Type); }
        }

        List<Identifier> types = [];

        foreach (Identifier type in _types)
        {
            if (available.Contains(type)) { types.Add(type); }
        }

        return types;
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
        HashSet<string> seen = new(PackageLabels.KeyComparer);
        HashSet<Identifier> types = [];

        foreach (AfflictionPrefab prefab in AfflictionPrefab.Prefabs)
        {
            if (prefab is null || prefab.Identifier.IsEmpty) { continue; }

            list.Add(new AfflictionPrefabEntry(
                prefab,
                PackageLabels.KeyOf(prefab.ContentPackage),
                prefab.AfflictionType,
                prefab.IsBuff,
                prefab.LimbSpecific));

            types.Add(prefab.AfflictionType);

            if (prefab.ContentPackage is { } package && seen.Add(package.Path)) { found.Add((package, package.Name)); }
        }

        list.Sort(static (a, b) =>
        {
            int order = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

            return order != 0 ? order : string.Compare(a.Identifier, b.Identifier, StringComparison.OrdinalIgnoreCase);
        });

        _entries = [.. list];
        _packages = PackageLabels.Build(found);
        _types = [.. types.OrderBy(type => type.Value, StringComparer.OrdinalIgnoreCase)];
    }
}

internal static class AfflictionPrefabLookup
{
    internal static AfflictionPrefab? By(string? identifier)
        => !string.IsNullOrWhiteSpace(identifier) && AfflictionPrefab.Prefabs.TryGet(identifier, out AfflictionPrefab? prefab)
            ? prefab
            : null;
}
