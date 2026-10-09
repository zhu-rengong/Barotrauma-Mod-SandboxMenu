using System.ComponentModel;
using System.Diagnostics;

namespace ContentPackageBuilder;

internal sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);

internal static class GitArchive
{
    private const string GitExecutableName = "git";

    internal static bool TryGetCommitId(string repoRoot, out string commitId, out string error)
    {
        var result = Run(repoRoot, ["rev-parse", "--short", "HEAD"]);

        if (result.ExitCode != 0)
        {
            commitId = string.Empty;
            error = DescribeFailure(result, "resolve HEAD");
            return false;
        }

        commitId = result.StandardOutput.Trim();

        if (commitId.Length == 0)
        {
            commitId = string.Empty;
            error = "git returned an empty commit id.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static bool TryPack(string repoRoot, string outputPath, out string error)
    {
        var result = Run(repoRoot, ["archive", "--format=zip", $"--output={outputPath}", "HEAD"]);

        if (result.ExitCode != 0)
        {
            error = DescribeFailure(result, "pack HEAD");
            return false;
        }

        if (!File.Exists(outputPath))
        {
            error = $"git archive reported success but '{outputPath}' was not created.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static bool HasUncommittedChanges(string repoRoot)
    {
        var result = Run(repoRoot, ["status", "--porcelain"]);
        return result.ExitCode == 0 && result.StandardOutput.Trim().Length > 0;
    }

    private static GitCommandResult Run(string repoRoot, string[] arguments)
    {
        var startInfo = new ProcessStartInfo(GitExecutableName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repoRoot);

        foreach (var argument in arguments)
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
            return new GitCommandResult(-1, string.Empty, $"Failed to start '{GitExecutableName}': {exception.Message}");
        }

        if (process is null)
        {
            return new GitCommandResult(-1, string.Empty, $"Failed to start '{GitExecutableName}'.");
        }

        using (process)
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            return new GitCommandResult(process.ExitCode, standardOutput.GetAwaiter().GetResult(), standardError.GetAwaiter().GetResult());
        }
    }

    private static string DescribeFailure(GitCommandResult result, string action)
    {
        var detail = result.StandardError.Trim();
        return detail.Length == 0
            ? $"Failed to {action} (git exit code {result.ExitCode})."
            : $"Failed to {action}: {detail}";
    }
}
