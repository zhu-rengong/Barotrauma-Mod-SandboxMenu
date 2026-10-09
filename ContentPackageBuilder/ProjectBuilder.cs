using System.ComponentModel;
using System.Diagnostics;

namespace ContentPackageBuilder;

internal sealed record ModBuildResult(ModProject Project, int ExitCode)
{
    internal bool Succeeded => ExitCode == 0;
}

internal static class ProjectBuilder
{
    private const string DotnetExecutableName = "dotnet";

    internal static string FormatCommand(ModProject project, BuildConfiguration configuration, string projectPath) =>
        $"{DotnetExecutableName} {string.Join(' ', BuildArguments(project, configuration, projectPath).Select(Quote))}";

    internal static async Task<ModBuildResult> BuildAsync(ModProject project, BuildConfiguration configuration, string projectPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(DotnetExecutableName)
        {
            UseShellExecute = false,
        };

        foreach (var argument in BuildArguments(project, configuration, projectPath))
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process? process;

        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"Failed to start '{DotnetExecutableName}': {exception.Message}");
            return new ModBuildResult(project, -1);
        }

        if (process is null)
        {
            Console.Error.WriteLine($"Failed to start '{DotnetExecutableName}'.");
            return new ModBuildResult(project, -1);
        }

        using (process)
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new ModBuildResult(project, process.ExitCode);
        }
    }

    private static IEnumerable<string> BuildArguments(ModProject project, BuildConfiguration configuration, string projectPath)
    {
        yield return "build";
        yield return projectPath;
        yield return "--configuration";
        yield return configuration.ToString();
        yield return "--runtime";
        yield return project.RuntimeIdentifier;
        yield return "/p:Platform=AnyCPU";
        yield return "-p:RuntimeFrameworkVersion=8.0.0";
        yield return "-clp:ErrorsOnly;Summary";
    }

    private static string Quote(string value) => value.Contains(' ') ? $"\"{value}\"" : value;
}
