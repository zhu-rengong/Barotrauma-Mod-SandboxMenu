using System.Collections.Frozen;

namespace ContentPackageBuilder;

[Flags]
internal enum BuildPlatform : byte
{
    None = 0,
    Windows = 1 << 0,
    Linux = 1 << 1,
    Mac = 1 << 2,
    All = Windows | Linux | Mac,
}

[Flags]
internal enum BuildTargetKind : byte
{
    None = 0,
    Client = 1 << 0,
    Server = 1 << 1,
    All = Client | Server,
}

internal enum BuildConfiguration : byte
{
    Debug = 0,
    Release = 1,
}

internal sealed record PlatformInfo(string Name, string RuntimeIdentifier);

internal sealed record ModProject(string ProjectPath, string RuntimeIdentifier, string PlatformName, BuildPlatform Platform, BuildTargetKind Target)
{
    internal string DisplayName => $"{PlatformName} {Target}";
}

internal static class BuildMatrix
{
    private static readonly BuildPlatform[] PlatformOrder = [BuildPlatform.Windows, BuildPlatform.Linux, BuildPlatform.Mac];

    private static readonly BuildTargetKind[] TargetOrder = [BuildTargetKind.Client, BuildTargetKind.Server];

    private static readonly FrozenDictionary<BuildPlatform, PlatformInfo> PlatformInfos = new Dictionary<BuildPlatform, PlatformInfo>
    {
        [BuildPlatform.Windows] = new("Windows", "win-x64"),
        [BuildPlatform.Linux] = new("Linux", "linux-x64"),
        [BuildPlatform.Mac] = new("Mac", "osx-x64"),
    }.ToFrozenDictionary();

    internal static IReadOnlyList<ModProject> Select(BuildPlatform platforms, BuildTargetKind targets) =>
    [
        .. PlatformOrder
            .Where(platform => (platforms & platform) != 0)
            .SelectMany(platform => TargetOrder
                .Where(target => (targets & target) != 0)
                .Select(target => CreateProject(platform, target))),
    ];

    internal static string Format(BuildPlatform platforms)
    {
        var names = PlatformOrder
            .Where(platform => (platforms & platform) != 0)
            .Select(platform => PlatformInfos[platform].Name)
            .ToArray();

        return names.Length == 0 ? "None" : string.Join(", ", names);
    }

    internal static string Format(BuildTargetKind targets)
    {
        var names = TargetOrder
            .Where(target => (targets & target) != 0)
            .Select(target => target.ToString())
            .ToArray();

        return names.Length == 0 ? "None" : string.Join(", ", names);
    }

    internal static bool TryParsePlatforms(string value, out BuildPlatform platforms, out string error)
    {
        platforms = BuildPlatform.None;

        foreach (var token in SplitTokens(value))
        {
            var platform = token.ToLowerInvariant() switch
            {
                "windows" or "win" or "win-x64" => BuildPlatform.Windows,
                "linux" or "linux-x64" => BuildPlatform.Linux,
                "mac" or "macos" or "osx" or "osx-x64" => BuildPlatform.Mac,
                "all" => BuildPlatform.All,
                _ => BuildPlatform.None,
            };

            if (platform is BuildPlatform.None)
            {
                error = $"Unknown platform '{token}'. Expected Windows, Linux, Mac or All.";
                return false;
            }

            platforms |= platform;
        }

        if (platforms is BuildPlatform.None)
        {
            error = "No platform was specified.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static bool TryParseTargets(string value, out BuildTargetKind targets, out string error)
    {
        targets = BuildTargetKind.None;

        foreach (var token in SplitTokens(value))
        {
            var target = token.ToLowerInvariant() switch
            {
                "client" => BuildTargetKind.Client,
                "server" => BuildTargetKind.Server,
                "all" => BuildTargetKind.All,
                _ => BuildTargetKind.None,
            };

            if (target is BuildTargetKind.None)
            {
                error = $"Unknown target '{token}'. Expected Client, Server or All.";
                return false;
            }

            targets |= target;
        }

        if (targets is BuildTargetKind.None)
        {
            error = "No target was specified.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static bool TryParseConfiguration(string value, out BuildConfiguration configuration, out string error)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "debug":
                configuration = BuildConfiguration.Debug;
                error = string.Empty;
                return true;
            case "release":
                configuration = BuildConfiguration.Release;
                error = string.Empty;
                return true;
            default:
                configuration = BuildConfiguration.Release;
                error = $"Unknown configuration '{value}'. Expected Debug or Release.";
                return false;
        }
    }

    private static ModProject CreateProject(BuildPlatform platform, BuildTargetKind target)
    {
        var info = PlatformInfos[platform];
        var projectDirectory = target is BuildTargetKind.Client ? "ClientProject" : "ServerProject";
        return new ModProject($"{projectDirectory}/{info.Name}{target}.csproj", info.RuntimeIdentifier, info.Name, platform, target);
    }

    private static IEnumerable<string> SplitTokens(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
