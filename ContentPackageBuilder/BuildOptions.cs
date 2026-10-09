namespace ContentPackageBuilder;

internal enum ParseOutcome : byte
{
    Success = 0,
    HelpRequested = 1,
    Error = 2,
}

internal sealed record BuildOptions(
    BuildPlatform? Platforms,
    BuildTargetKind? Targets,
    BuildConfiguration? Configuration,
    bool? IncludeBin,
    bool? PackGit,
    bool? SkipBuild,
    bool DryRun,
    bool Yes)
{
    internal static string Usage =>
        """
        ContentPackageBuilder - builds the mod projects and mirrors Content to ModDeployDir.

        Usage:
          ContentPackageBuilder [options]

        Options:
          --platforms <list>      Platforms to build: Windows, Linux, Mac. Comma-separated, default all.
          --targets <list>        Targets to build: Client, Server. Comma-separated, default all.
          --configuration <name>  Build configuration: Debug or Release. Default Release.
          --skip-build            Skip the build stage and only run the content sync.
          --include-bin           Include Content/bin (build outputs) in the sync.
          --exclude-bin           Exclude Content/bin from the sync.
          --pack-git              Pack the current git commit into Content/<ModName>.zip. Default.
          --no-pack               Do not pack the current git commit.
          --yes, -y               Skip all prompts. Unspecified options fall back to their defaults:
                                  all platforms, all targets, Release, bin included, commit packed.
          --dry-run               Print the build commands and the full sync plan without writing anything.
          --help, -h              Print this help.

        Deployment target:
          ModDeployDir from UserBuildData.props in the repository root.

        Exit codes:
          0  success
          1  usage error, missing configuration, or cancelled by the user
          3  build failed, sync skipped
          4  sync failed
        """;

    internal static ParseOutcome TryParse(string[] arguments, out BuildOptions options, out string error)
    {
        options = Default;
        error = string.Empty;

        BuildPlatform? platforms = null;
        BuildTargetKind? targets = null;
        BuildConfiguration? configuration = null;
        bool? includeBin = null;
        bool? packGit = null;
        bool? skipBuild = null;
        var dryRun = false;
        var yes = false;

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            var name = argument;
            string? inlineValue = null;

            var separatorIndex = argument.IndexOf('=');

            if (separatorIndex > 0)
            {
                name = argument[..separatorIndex];
                inlineValue = argument[(separatorIndex + 1)..];
            }

            switch (name)
            {
                case "--help" or "-h" or "/?":
                    return ParseOutcome.HelpRequested;

                case "--yes" or "-y":
                    yes = true;
                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                case "--include-bin":
                    includeBin = true;
                    break;

                case "--exclude-bin":
                    includeBin = false;
                    break;

                case "--pack-git":
                    packGit = true;
                    break;

                case "--no-pack":
                    packGit = false;
                    break;

                case "--skip-build":
                    skipBuild = true;
                    break;

                case "--platforms":
                    if (!TryTakeValue(inlineValue, arguments, ref index, name, out var platformValue, out error)
                        || !BuildMatrix.TryParsePlatforms(platformValue, out var parsedPlatforms, out error))
                    {
                        return ParseOutcome.Error;
                    }

                    platforms = parsedPlatforms;
                    break;

                case "--targets":
                    if (!TryTakeValue(inlineValue, arguments, ref index, name, out var targetValue, out error)
                        || !BuildMatrix.TryParseTargets(targetValue, out var parsedTargets, out error))
                    {
                        return ParseOutcome.Error;
                    }

                    targets = parsedTargets;
                    break;

                case "--configuration":
                    if (!TryTakeValue(inlineValue, arguments, ref index, name, out var configurationValue, out error)
                        || !BuildMatrix.TryParseConfiguration(configurationValue, out var parsedConfiguration, out error))
                    {
                        return ParseOutcome.Error;
                    }

                    configuration = parsedConfiguration;
                    break;

                default:
                    error = $"Unknown argument '{argument}'.";
                    return ParseOutcome.Error;
            }
        }

        options = new BuildOptions(platforms, targets, configuration, includeBin, packGit, skipBuild, dryRun, yes);
        return ParseOutcome.Success;
    }

    internal BuildOptions CompleteInteractively()
    {
        var skipBuild = SkipBuild ?? AskWhetherToSkipBuild();

        if (skipBuild)
        {
            return this with { SkipBuild = true };
        }

        var platforms = Platforms ?? AskPlatforms();
        var targets = Targets ?? AskTargets();
        var configuration = Configuration ?? AskConfiguration();
        return this with { Platforms = platforms, Targets = targets, Configuration = configuration, SkipBuild = false };
    }

    internal BuildOptions WithDefaults()
    {
        var platforms = Platforms ?? BuildPlatform.All;
        var targets = Targets ?? BuildTargetKind.All;
        var configuration = Configuration ?? BuildConfiguration.Release;
        var includeBin = IncludeBin ?? true;
        var packGit = PackGit ?? true;
        var skipBuild = SkipBuild ?? false;
        return this with { Platforms = platforms, Targets = targets, Configuration = configuration, IncludeBin = includeBin, PackGit = packGit, SkipBuild = skipBuild };
    }

    private static BuildOptions Default => new(null, null, null, null, null, null, false, false);

    private bool AskWhetherToSkipBuild() =>
        Platforms is null && Targets is null && Configuration is null
            ? !ConsolePrompt.AskYesNo("Build the mod projects first?", true)
            : false;

    private static BuildPlatform AskPlatforms()
    {
        while (true)
        {
            var value = ConsolePrompt.AskLine("Platforms to build: Windows, Linux, Mac (comma-separated)", "all");

            if (BuildMatrix.TryParsePlatforms(value, out var platforms, out var error))
            {
                return platforms;
            }

            Console.WriteLine(error);
        }
    }

    private static BuildTargetKind AskTargets()
    {
        while (true)
        {
            var value = ConsolePrompt.AskLine("Targets to build: Client, Server (comma-separated)", "all");

            if (BuildMatrix.TryParseTargets(value, out var targets, out var error))
            {
                return targets;
            }

            Console.WriteLine(error);
        }
    }

    private static BuildConfiguration AskConfiguration()
    {
        while (true)
        {
            var value = ConsolePrompt.AskLine("Build configuration: Debug or Release", "Release");

            if (BuildMatrix.TryParseConfiguration(value, out var configuration, out var error))
            {
                return configuration;
            }

            Console.WriteLine(error);
        }
    }

    private static bool TryTakeValue(string? inlineValue, string[] arguments, ref int index, string name, out string value, out string error)
    {
        if (!string.IsNullOrEmpty(inlineValue))
        {
            value = inlineValue;
            error = string.Empty;
            return true;
        }

        if (index + 1 < arguments.Length)
        {
            value = arguments[++index];
            error = string.Empty;
            return true;
        }

        value = string.Empty;
        error = $"Missing value for '{name}'.";
        return false;
    }
}

internal static class ConsolePrompt
{
    internal static string AskLine(string question, string defaultValue)
    {
        Console.Write($"{question} [{defaultValue}]: ");
        var answer = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(answer))
        {
            Console.WriteLine(defaultValue);
            return defaultValue;
        }

        return answer;
    }

    internal static bool AskYesNo(string question, bool defaultValue)
    {
        var hint = defaultValue ? "Y/n" : "y/N";

        while (true)
        {
            Console.Write($"{question} [{hint}]: ");
            var answer = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(answer))
            {
                Console.WriteLine(defaultValue ? "Yes" : "No");
                return defaultValue;
            }

            if (Matches(answer, "y") || Matches(answer, "yes"))
            {
                return true;
            }

            if (Matches(answer, "n") || Matches(answer, "no"))
            {
                return false;
            }

            Console.WriteLine("Please answer 'y' or 'n'.");
        }
    }

    private static bool Matches(string value, string candidate) => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase);
}
