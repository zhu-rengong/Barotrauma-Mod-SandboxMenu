namespace SandboxMenu.Domain.Prefabs;

internal sealed record PackageOption(ContentPackage Package, string Label);

internal static class PackageOptions
{
    internal static IReadOnlyList<PackageOption> Build(IEnumerable<ContentPackage?> packages)
    {
        List<(ContentPackage Package, string Name)> found = [];
        HashSet<ContentPackage> seen = new(ReferenceEqualityComparer.Instance);

        foreach (ContentPackage? package in packages)
        {
            if (package is null || package.Path.Length == 0 || !seen.Add(package)) { continue; }

            found.Add((package, package.Name));
        }

        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);

        foreach ((ContentPackage _, string name) in found) { counts[name] = counts.GetValueOrDefault(name) + 1; }

        List<PackageOption> options = new(found.Count);

        foreach ((ContentPackage package, string name) in found)
        {
            string folder = FolderOf(package.Path);

            string label = name.Length == 0
                ? folder
                : counts[name] > 1 ? $"{name} ({folder})" : name;

            options.Add(new PackageOption(package, label));
        }

        options.Sort(static (a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

        return options;
    }

    private static string FolderOf(string path)
    {
        string folder = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);

        return folder.Length == 0 ? path : folder;
    }
}
