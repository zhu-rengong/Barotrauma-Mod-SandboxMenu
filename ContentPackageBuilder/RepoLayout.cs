using System.Diagnostics.CodeAnalysis;

namespace ContentPackageBuilder;

internal sealed record RepoLayout(string Root, string ContentDirectory, string BinDirectory, string UserBuildDataPath)
{
    private const string BuildDataFileName = "BuildData.props";
    private const string ContentPackageBuilderDirectoryName = "ContentPackageBuilder";
    private const string ContentDirectoryName = "Content";
    private const string BinDirectoryName = "bin";
    private const string UserBuildDataFileName = "UserBuildData.props";

    internal bool ContentDirectoryExists => Directory.Exists(ContentDirectory);

    internal string BuildDataPath => Path.Combine(Root, BuildDataFileName);

    internal static bool TryResolve([NotNullWhen(true)] out RepoLayout? layout)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, BuildDataFileName)))
            {
                continue;
            }

            var contentDirectory = Path.Combine(directory.FullName, ContentPackageBuilderDirectoryName, ContentDirectoryName);
            layout = new RepoLayout(
                directory.FullName,
                contentDirectory,
                Path.Combine(contentDirectory, BinDirectoryName),
                Path.Combine(directory.FullName, UserBuildDataFileName));
            return true;
        }

        layout = null;
        return false;
    }

    internal string GetProjectPath(ModProject project) => Path.GetFullPath(Path.Combine(Root, project.ProjectPath));
}
