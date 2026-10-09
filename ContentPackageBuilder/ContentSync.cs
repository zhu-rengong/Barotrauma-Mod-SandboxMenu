using System.Diagnostics.CodeAnalysis;

namespace ContentPackageBuilder;

internal enum SyncAction : byte
{
    Add = 0,
    Update = 1,
    Delete = 2,
}

internal sealed record SyncItem(SyncAction Action, string RelativePath, long? SourceLength, long? TargetLength);

internal sealed record SyncPlan(IReadOnlyList<SyncItem> Items)
{
    internal int AddCount => Items.Count(item => item.Action is SyncAction.Add);

    internal int UpdateCount => Items.Count(item => item.Action is SyncAction.Update);

    internal int DeleteCount => Items.Count(item => item.Action is SyncAction.Delete);
}

internal sealed record SyncRequest(string SourceDirectory, string BinDirectory, string TargetDirectory, bool IncludeBin, string RepoRoot);

internal static class ContentSync
{
    private const string PathSeparator = "/";
    private const int CopyAttempts = 3;

    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CopyRetryDelay = TimeSpan.FromSeconds(1);

    private static StringComparer PathComparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal static bool TryValidate(SyncRequest request, [NotNullWhen(false)] out string? error)
    {
        var source = Normalize(request.SourceDirectory);
        var target = Normalize(request.TargetDirectory);

        if (!Directory.Exists(request.SourceDirectory))
        {
            error = $"Source directory '{source}' does not exist.";
            return false;
        }

        if (PathComparer.Equals(source, target))
        {
            error = "Target directory must differ from the source directory.";
            return false;
        }

        if (IsWithin(source, target) || IsWithin(target, source))
        {
            error = "Target directory must not contain or be contained by the source directory.";
            return false;
        }

        if (PathComparer.Equals(target, Normalize(request.RepoRoot)))
        {
            error = "Target directory must not be the repository root.";
            return false;
        }

        var root = Path.GetPathRoot(target);

        if (string.IsNullOrEmpty(root) || PathComparer.Equals(target, Normalize(root)))
        {
            error = "Target directory must not be a file system root.";
            return false;
        }

        error = null;
        return true;
    }

    internal static SyncPlan CreatePlan(SyncRequest request, IReadOnlyList<string>? pendingFiles = null)
    {
        var sourceFiles = EnumerateFiles(request.SourceDirectory, request);
        var targetFiles = EnumerateFiles(request.TargetDirectory, request);
        var pending = new HashSet<string>((pendingFiles ?? []).Select(path => path.Replace('\\', '/')), PathComparer);
        var items = new List<SyncItem>();

        foreach (var (relativePath, source) in sourceFiles)
        {
            if (targetFiles.TryGetValue(relativePath, out var target))
            {
                if (NeedsCopy(source, target))
                {
                    items.Add(new SyncItem(SyncAction.Update, relativePath, source.Length, target.Length));
                }

                continue;
            }

            items.Add(new SyncItem(SyncAction.Add, relativePath, source.Length, null));
        }

        foreach (var (relativePath, target) in targetFiles)
        {
            if (!sourceFiles.ContainsKey(relativePath) && !pending.Contains(relativePath))
            {
                items.Add(new SyncItem(SyncAction.Delete, relativePath, null, target.Length));
            }
        }

        foreach (var relativePath in pending)
        {
            items.RemoveAll(item => item.Action is not SyncAction.Delete && PathComparer.Equals(item.RelativePath, relativePath));
            items.Add(targetFiles.TryGetValue(relativePath, out var target)
                ? new SyncItem(SyncAction.Update, relativePath, null, target.Length)
                : new SyncItem(SyncAction.Add, relativePath, null, null));
        }

        return new SyncPlan([.. items.OrderBy(item => item.Action).ThenBy(item => item.RelativePath, PathComparer)]);
    }

    internal static void Print(SyncRequest request, SyncPlan plan)
    {
        Console.WriteLine("=== Content Sync ===");
        Console.WriteLine($"Source      : {Normalize(request.SourceDirectory)}");
        Console.WriteLine($"Target      : {Normalize(request.TargetDirectory)}{(Directory.Exists(request.TargetDirectory) ? string.Empty : " (will be created)")}");
        Console.WriteLine($"Content/bin : {(request.IncludeBin ? "included" : "excluded")}");
        Console.WriteLine();

        PrintGroup(SyncAction.Add, "New", plan);
        PrintGroup(SyncAction.Update, "Updated", plan);
        PrintGroup(SyncAction.Delete, "Removed", plan);

        Console.WriteLine();
        Console.WriteLine($"Total: {plan.Items.Count} item(s) - {plan.AddCount} new, {plan.UpdateCount} updated, {plan.DeleteCount} removed");
    }

    internal static IReadOnlyList<string> Apply(SyncRequest request, SyncPlan plan)
    {
        var failures = new List<string>();
        var targetRoot = Normalize(request.TargetDirectory);

        foreach (var item in plan.Items)
        {
            var targetPath = Path.Combine(request.TargetDirectory, item.RelativePath);

            if (item.Action is SyncAction.Delete)
            {
                DeleteItem(targetRoot, item, targetPath, failures);
                continue;
            }

            var sourcePath = Path.Combine(request.SourceDirectory, item.RelativePath);

            if (TryCopy(sourcePath, targetPath, out var copyError))
            {
                Console.WriteLine($"  {(item.Action is SyncAction.Add ? "+" : "~")} {item.RelativePath}");
                continue;
            }

            failures.Add($"Failed to copy '{item.RelativePath}': {copyError}");
        }

        RemoveEmptyDirectories(request, failures);
        return failures;
    }

    private static void DeleteItem(string targetRoot, SyncItem item, string targetPath, List<string> failures)
    {
        var fullPath = Path.GetFullPath(targetPath);

        if (!IsWithin(targetRoot, fullPath))
        {
            failures.Add($"Refusing to delete '{fullPath}': outside the target directory.");
            return;
        }

        try
        {
            File.Delete(fullPath);
            Console.WriteLine($"  - {item.RelativePath}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failures.Add($"Failed to delete '{item.RelativePath}': {exception.Message}");
        }
    }

    private static bool TryCopy(string sourcePath, string targetPath, [NotNullWhen(false)] out string? error)
    {
        error = null;

        for (var attempt = 1; attempt <= CopyAttempts; attempt++)
        {
            try
            {
                var directory = Path.GetDirectoryName(targetPath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(sourcePath, targetPath, overwrite: true);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                error = exception.Message;

                if (attempt < CopyAttempts)
                {
                    Thread.Sleep(CopyRetryDelay);
                }
            }
        }

        error ??= "Copy failed without a reported error.";
        return false;
    }

    private static void RemoveEmptyDirectories(SyncRequest request, List<string> failures)
    {
        if (!Directory.Exists(request.TargetDirectory))
        {
            return;
        }

        var binRelativePath = GetBinRelativePath(request);

        var directories = Directory
            .EnumerateDirectories(request.TargetDirectory, "*", SearchOption.AllDirectories)
            .OrderByDescending(directory => directory.Count(character => character is '/' or '\\'))
            .ThenByDescending(directory => directory.Length);

        foreach (var directory in directories)
        {
            var relativePath = GetRelativePath(request.TargetDirectory, directory);

            if (!request.IncludeBin && IsBinPath(relativePath, binRelativePath))
            {
                continue;
            }

            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures.Add($"Failed to remove empty directory '{relativePath}': {exception.Message}");
            }
        }
    }

    private static Dictionary<string, FileInfo> EnumerateFiles(string root, SyncRequest request)
    {
        var files = new Dictionary<string, FileInfo>(PathComparer);

        if (!Directory.Exists(root))
        {
            return files;
        }

        var binRelativePath = GetBinRelativePath(request);

        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relativePath = GetRelativePath(root, path);

            if (!request.IncludeBin && IsBinPath(relativePath, binRelativePath))
            {
                continue;
            }

            files[relativePath] = new FileInfo(path);
        }

        return files;
    }

    private static void PrintGroup(SyncAction action, string title, SyncPlan plan)
    {
        var items = plan.Items.Where(item => item.Action == action).ToArray();
        Console.WriteLine($"{title} ({items.Length}):");

        if (items.Length == 0)
        {
            Console.WriteLine("  (none)");
            return;
        }

        foreach (var item in items)
        {
            Console.WriteLine($"  {DescribeItem(item)}");
        }
    }

    private static string DescribeItem(SyncItem item) => item switch
    {
        { Action: SyncAction.Add, SourceLength: null } => $"+ {item.RelativePath} (pending)",
        { Action: SyncAction.Add } => $"+ {item.RelativePath} ({item.SourceLength} bytes)",
        { Action: SyncAction.Update, SourceLength: null } => $"~ {item.RelativePath} (pending)",
        { Action: SyncAction.Update } => $"~ {item.RelativePath} ({item.TargetLength} -> {item.SourceLength} bytes)",
        _ => $"- {item.RelativePath} ({item.TargetLength} bytes)",
    };

    private static bool NeedsCopy(FileInfo source, FileInfo target) =>
        source.Length != target.Length
        || Math.Abs((source.LastWriteTimeUtc - target.LastWriteTimeUtc).TotalSeconds) > TimestampTolerance.TotalSeconds;

    private static string GetRelativePath(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string GetBinRelativePath(SyncRequest request) => GetRelativePath(request.SourceDirectory, request.BinDirectory);

    private static bool IsBinPath(string relativePath, string binRelativePath) =>
        relativePath.Equals(binRelativePath, StringComparison.OrdinalIgnoreCase)
        || relativePath.StartsWith(binRelativePath + PathSeparator, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsWithin(string parent, string candidate)
    {
        var root = Path.TrimEndingDirectorySeparator(parent);
        return candidate.Length > root.Length
            && candidate.StartsWith(root, PathComparison)
            && candidate[root.Length] is '/' or '\\';
    }
}
