namespace ContentPackageBuilder;

internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;
    private const int ExitBuildFailed = 3;
    private const int ExitSyncFailed = 4;

    private const string DeployDirectoryElementName = "ModDeployDir";
    private const string ModNameElementName = "ModName";

    private static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();

        ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.CancelKeyPress += onCancel;

        try
        {
            return await RunAsync(args, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine("Cancelled.");
            return ExitFailure;
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    private static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var outcome = BuildOptions.TryParse(args, out var options, out var parseError);

        if (outcome is ParseOutcome.HelpRequested)
        {
            Console.WriteLine(BuildOptions.Usage);
            return ExitSuccess;
        }

        if (outcome is ParseOutcome.Error)
        {
            Console.Error.WriteLine(parseError);
            Console.Error.WriteLine();
            Console.Error.WriteLine(BuildOptions.Usage);
            return ExitFailure;
        }

        if (!RepoLayout.TryResolve(out var layout))
        {
            Console.Error.WriteLine("Could not locate the repository root: no 'BuildData.props' found above the executable.");
            return ExitFailure;
        }

        if (!layout.ContentDirectoryExists)
        {
            Console.Error.WriteLine($"Content directory '{layout.ContentDirectory}' does not exist.");
            return ExitFailure;
        }

        string deployDirectory;

        try
        {
            deployDirectory = PropsFile.ReadDirectory(layout.UserBuildDataPath, DeployDirectoryElementName);
        }
        catch (BuildDataException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return ExitFailure;
        }

        var interactive = !options.Yes && !options.DryRun;

        if (interactive && Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Standard input is redirected; use --yes (or --dry-run) for non-interactive runs.");
            return ExitFailure;
        }

        options = interactive ? options.CompleteInteractively() : options.WithDefaults();

        var platforms = options.Platforms ?? BuildPlatform.All;
        var targets = options.Targets ?? BuildTargetKind.All;
        var configuration = options.Configuration ?? BuildConfiguration.Release;

        Console.WriteLine("=== Build ===");

        if (options.SkipBuild ?? false)
        {
            Console.WriteLine("Skipped.");
            Console.WriteLine();
        }
        else
        {
            var projects = BuildMatrix.Select(platforms, targets);

            if (projects.Count == 0)
            {
                Console.Error.WriteLine("No projects match the selected platforms and targets.");
                return ExitFailure;
            }

            Console.WriteLine($"Configuration : {configuration}");
            Console.WriteLine($"Platforms     : {BuildMatrix.Format(platforms)}");
            Console.WriteLine($"Targets       : {BuildMatrix.Format(targets)}");
            Console.WriteLine($"Projects      : {projects.Count}");

            foreach (var project in projects)
            {
                Console.WriteLine($"  {project.ProjectPath} [{project.RuntimeIdentifier}]");
            }

            Console.WriteLine();

            if (options.DryRun)
            {
                foreach (var project in projects)
                {
                    Console.WriteLine(ProjectBuilder.FormatCommand(project, configuration, layout.GetProjectPath(project)));
                }

                Console.WriteLine();
            }
            else if (interactive && !ConsolePrompt.AskYesNo($"Build {projects.Count} project(s)?", true))
            {
                Console.WriteLine("Build skipped; continuing with the sync.");
                Console.WriteLine();
            }
            else
            {
                var results = new List<ModBuildResult>(projects.Count);

                foreach (var project in projects)
                {
                    Console.WriteLine($"--- {project.DisplayName} ---");
                    var result = await ProjectBuilder.BuildAsync(project, configuration, layout.GetProjectPath(project), cancellationToken).ConfigureAwait(false);
                    results.Add(result);
                    Console.WriteLine(result.Succeeded
                        ? $"--- {project.DisplayName}: succeeded ---"
                        : $"--- {project.DisplayName}: failed (exit code {result.ExitCode}) ---");
                    Console.WriteLine();
                }

                var failedProjects = results.Where(result => !result.Succeeded).Select(result => result.Project.DisplayName).ToArray();

                if (failedProjects.Length > 0)
                {
                    Console.Error.WriteLine($"Build failed for {failedProjects.Length} of {results.Count} project(s): {string.Join(", ", failedProjects)}");
                    Console.Error.WriteLine("Sync skipped.");
                    return ExitBuildFailed;
                }
            }
        }

        if (!TryPrepareCommitArchive(options, layout, interactive, out var pendingFiles))
        {
            return ExitFailure;
        }

        var includeBin = options.IncludeBin ?? ConsolePrompt.AskYesNo("Include Content/bin (build outputs) in the sync?", true);
        var request = new SyncRequest(layout.ContentDirectory, layout.BinDirectory, deployDirectory, includeBin, layout.Root);

        if (!ContentSync.TryValidate(request, out var validationError))
        {
            Console.Error.WriteLine(validationError);
            return ExitFailure;
        }

        var plan = ContentSync.CreatePlan(request, pendingFiles);
        Console.WriteLine();
        ContentSync.Print(request, plan);

        if (plan.Items.Count == 0)
        {
            Console.WriteLine("Already up to date.");
            return ExitSuccess;
        }

        if (options.DryRun)
        {
            Console.WriteLine();
            Console.WriteLine("Dry run: nothing was written.");
            return ExitSuccess;
        }

        if (interactive && !ConsolePrompt.AskYesNo("Proceed with sync?", true))
        {
            Console.WriteLine("Cancelled.");
            return ExitFailure;
        }

        Console.WriteLine();
        Console.WriteLine("Syncing...");

        var failures = ContentSync.Apply(request, plan);

        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            Console.Error.WriteLine($"Sync failed: {failures.Count} error(s).");
            return ExitSyncFailed;
        }

        Console.WriteLine($"Sync complete: {plan.AddCount} added, {plan.UpdateCount} updated, {plan.DeleteCount} removed.");
        Console.WriteLine($"Deployed to {Path.TrimEndingDirectorySeparator(Path.GetFullPath(deployDirectory))}");
        return ExitSuccess;
    }

    private static bool TryPrepareCommitArchive(BuildOptions options, RepoLayout layout, bool interactive, out IReadOnlyList<string> pendingFiles)
    {
        pendingFiles = [];

        if (options.PackGit is false)
        {
            return true;
        }

        string modName;

        try
        {
            modName = PropsFile.ReadElementValue(layout.BuildDataPath, ModNameElementName);
        }
        catch (BuildDataException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return false;
        }

        var archiveName = $"{modName}.zip";

        if (options.PackGit is null && interactive && !ConsolePrompt.AskYesNo($"Pack the current git commit into Content/{archiveName}?", true))
        {
            return true;
        }

        if (!GitArchive.TryGetCommitId(layout.Root, out var commitId, out var commitError))
        {
            Console.Error.WriteLine(commitError);
            return false;
        }

        if (GitArchive.HasUncommittedChanges(layout.Root))
        {
            Console.WriteLine($"Note: the working tree has uncommitted changes; only HEAD ({commitId}) is packed.");
        }

        if (options.DryRun)
        {
            Console.WriteLine($"Would pack commit {commitId} into Content/{archiveName}.");
            pendingFiles = [archiveName];
            return true;
        }

        var archivePath = Path.Combine(layout.ContentDirectory, archiveName);

        try
        {
            File.Delete(archivePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Failed to replace '{archivePath}': {exception.Message}");
            return false;
        }

        if (!GitArchive.TryPack(layout.Root, archivePath, out var packError))
        {
            Console.Error.WriteLine(packError);
            return false;
        }

        Console.WriteLine($"Packed commit {commitId} into Content/{archiveName} ({new FileInfo(archivePath).Length} bytes).");
        return true;
    }
}
