using System.Collections.Immutable;

namespace SandboxMenu.Domain.Prefabs;

internal sealed record TalentSubTreeOption(Identifier Identifier, LocalizedString Label);

internal sealed record TalentJobOption(
    Identifier Identifier,
    LocalizedString Label,
    Color Accent,
    IReadOnlyList<TalentSubTreeOption> SubTrees);

internal sealed record TalentPrefabEntry(
    TalentPrefab Prefab,
    string PackageKey,
    ImmutableArray<Identifier> Jobs,
    ImmutableArray<Identifier> SubTrees,
    bool Hidden)
{
    internal string Identifier => Prefab.Identifier.Value;

    internal string Name => Prefab.DisplayName.Value is { Length: > 0 } name ? name : Identifier;

    internal bool Matches(string text)
        => Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Identifier.Contains(text, StringComparison.OrdinalIgnoreCase)
            || (Prefab.Description.Value?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
}

internal sealed record TalentFilter(
    IReadOnlySet<string> Packages,
    IReadOnlySet<Identifier> Jobs,
    IReadOnlySet<Identifier> SubTrees)
{
    internal bool Allows(TalentPrefabEntry entry)
        => (Packages.Count == 0 || (entry.PackageKey.Length > 0 && Packages.Contains(entry.PackageKey)))
            && Matches(Jobs, entry.Jobs)
            && Matches(SubTrees, entry.SubTrees);

    private static bool Matches<T>(IReadOnlySet<T> selected, ImmutableArray<T> values)
    {
        if (selected.Count == 0) { return true; }

        foreach (T value in values)
        {
            if (selected.Contains(value)) { return true; }
        }

        return false;
    }
}

internal static class TalentPrefabCatalog
{
    private static ImmutableArray<TalentPrefabEntry> _entries = ImmutableArray<TalentPrefabEntry>.Empty;
    private static IReadOnlyList<PackageLabel> _packages = [];
    private static IReadOnlyList<TalentJobOption> _jobs = [];
    private static Dictionary<Identifier, TalentPrefabEntry> _byIdentifier = [];
    private static Dictionary<Identifier, LocalizedString> _subTreeLabels = [];

    internal static IReadOnlyList<TalentPrefabEntry> All()
    {
        EnsureBuilt();

        return _entries;
    }

    internal static IReadOnlyList<PackageLabel> Packages()
    {
        EnsureBuilt();

        return _packages;
    }

    internal static IReadOnlyList<TalentJobOption> Jobs()
    {
        EnsureBuilt();

        return _jobs;
    }

    internal static IReadOnlyList<TalentSubTreeOption> SubTreesIn(IReadOnlySet<Identifier> jobs)
    {
        EnsureBuilt();

        if (jobs.Count == 0) { return [.. _jobs.SelectMany(static job => job.SubTrees).DistinctBy(static subTree => subTree.Identifier)]; }

        List<TalentSubTreeOption> subTrees = [];

        foreach (TalentJobOption job in _jobs)
        {
            if (jobs.Contains(job.Identifier)) { subTrees.AddRange(job.SubTrees); }
        }

        return [.. subTrees.DistinctBy(static subTree => subTree.Identifier)];
    }

    internal static TalentPrefabEntry? ByIdentifier(Identifier identifier)
    {
        EnsureBuilt();

        return _byIdentifier.TryGetValue(identifier, out TalentPrefabEntry? entry) ? entry : null;
    }

    internal static LocalizedString SubTreeLabel(Identifier identifier)
    {
        EnsureBuilt();

        return _subTreeLabels.TryGetValue(identifier, out LocalizedString? label) && label is not null ? label : identifier.Value;
    }

    internal static void Reset()
    {
        _entries = ImmutableArray<TalentPrefabEntry>.Empty;
        _packages = [];
        _jobs = [];
        _byIdentifier = [];
        _subTreeLabels = [];
    }

    private static void EnsureBuilt()
    {
        if (!_entries.IsDefaultOrEmpty) { return; }

        Dictionary<Identifier, (LocalizedString Label, Color Accent, List<TalentSubTreeOption> SubTrees)> trees = [];
        Dictionary<Identifier, HashSet<Identifier>> jobsById = [];
        Dictionary<Identifier, HashSet<Identifier>> subTreesById = [];

        foreach (TalentTree tree in TalentTree.JobTalentTrees)
        {
            if (tree is null || tree.Identifier.IsEmpty) { continue; }

            JobPrefab? job = JobPrefab.Prefabs.TryGet(tree.Identifier, out JobPrefab? found) ? found : null;
            LocalizedString label = job?.Name is { } name && !string.IsNullOrWhiteSpace(name.Value) ? name : tree.Identifier.Value;
            Color accent = job?.UIColor ?? Color.White;

            if (!trees.TryGetValue(tree.Identifier, out var slot))
            {
                trees[tree.Identifier] = slot = (label, accent, []);
            }

            foreach (TalentSubTree subTree in tree.TalentSubTrees)
            {
                if (subTree is null || subTree.Identifier.IsEmpty) { continue; }

                TalentSubTreeOption option = new(subTree.Identifier, subTree.DisplayName);

                if (!slot.SubTrees.Exists(existing => existing.Identifier == subTree.Identifier)) { slot.SubTrees.Add(option); }

                foreach (Identifier talent in subTree.AllTalentIdentifiers)
                {
                    Add(jobsById, talent, tree.Identifier);
                    Add(subTreesById, talent, subTree.Identifier);
                }
            }
        }

        List<TalentJobOption> jobs = [];

        foreach ((Identifier identifier, (LocalizedString label, Color accent, List<TalentSubTreeOption> subTrees)) in trees)
        {
            subTrees.Sort(static (a, b) => string.Compare(a.Label.Value, b.Label.Value, StringComparison.OrdinalIgnoreCase));
            jobs.Add(new TalentJobOption(identifier, label, accent, subTrees));
        }

        jobs.Sort(static (a, b) => string.Compare(a.Label.Value, b.Label.Value, StringComparison.OrdinalIgnoreCase));

        List<TalentPrefabEntry> entries = [];

        foreach (TalentPrefab prefab in TalentPrefab.TalentPrefabs)
        {
            if (prefab is null || prefab.Identifier.IsEmpty) { continue; }

            entries.Add(new TalentPrefabEntry(
                prefab,
                PackageLabels.KeyOf(prefab.ContentPackage),
                ToSorted(jobsById, prefab.Identifier),
                ToSorted(subTreesById, prefab.Identifier),
                prefab.IsHiddenExtraTalent));
        }

        entries.Sort(static (a, b) =>
        {
            int order = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

            return order != 0 ? order : string.Compare(a.Identifier, b.Identifier, StringComparison.OrdinalIgnoreCase);
        });

        Dictionary<Identifier, TalentPrefabEntry> byIdentifier = new(entries.Count);
        Dictionary<Identifier, LocalizedString> subTreeLabels = [];

        foreach (TalentPrefabEntry entry in entries) { byIdentifier[entry.Prefab.Identifier] = entry; }

        foreach (TalentJobOption job in jobs)
        {
            foreach (TalentSubTreeOption subTree in job.SubTrees) { subTreeLabels[subTree.Identifier] = subTree.Label; }
        }

        _entries = [.. entries];
        _packages = PackageLabels.From(TalentPrefab.TalentPrefabs.Select(static prefab => prefab.ContentPackage));
        _jobs = jobs;
        _byIdentifier = byIdentifier;
        _subTreeLabels = subTreeLabels;
    }

    private static void Add(Dictionary<Identifier, HashSet<Identifier>> map, Identifier talent, Identifier owner)
    {
        if (!map.TryGetValue(talent, out HashSet<Identifier>? owners)) { map[talent] = owners = []; }

        owners.Add(owner);
    }

    private static ImmutableArray<Identifier> ToSorted(Dictionary<Identifier, HashSet<Identifier>> map, Identifier talent)
        => map.TryGetValue(talent, out HashSet<Identifier>? owners) && owners.Count > 0
            ? [.. owners.OrderBy(static owner => owner.Value, StringComparer.OrdinalIgnoreCase)]
            : ImmutableArray<Identifier>.Empty;
}
