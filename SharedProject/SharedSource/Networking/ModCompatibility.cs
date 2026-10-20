namespace SandboxMenu.Networking;

internal static class ModCompatibility
{
    private static readonly Lazy<(string Version, string Hash)> LocalIdentity = new(Resolve, LazyThreadSafetyMode.PublicationOnly);

    private static bool _unknownIdentityReported;
    private static bool _unknownClientReported;

    internal static string Version => LocalIdentity.Value.Version;

    internal static string Hash => LocalIdentity.Value.Hash;

    internal static bool Matches(ushort protocolVersion, string? version, string? hash, string clientName, string action)
    {
        if (protocolVersion != Protocol.Version)
        {
            Services.DebugConsole.AddWarning(
                $"{clientName} speaks version {protocolVersion} of the {action} protocol, but this server speaks {Protocol.Version}.");

            return false;
        }

        (string localVersion, string localHash) = LocalIdentity.Value;

        if (localVersion.Length == 0 || localHash.Length == 0)
        {
            ReportUnknownIdentity();
            return true;
        }

        string remoteVersion = version ?? string.Empty;
        string remoteHash = hash ?? string.Empty;

        if (remoteVersion.Length == 0 || remoteHash.Length == 0)
        {
            ReportUnknownClient(clientName);
            return true;
        }

        if (!string.Equals(localVersion, remoteVersion, StringComparison.Ordinal))
        {
            Services.DebugConsole.AddWarning(
                $"{clientName} runs sandbox menu {remoteVersion}, while this server runs {localVersion}.");

            return false;
        }

        if (!string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase))
        {
            Services.DebugConsole.AddWarning(
                $"{clientName} runs sandbox menu hash {Shorten(remoteHash)}, while this server runs {Shorten(localHash)}.");

            return false;
        }

        return true;
    }

    private static (string Version, string Hash) Resolve()
    {
        try
        {
            string? location = typeof(Plugin).Assembly.Location;

            if (string.IsNullOrEmpty(location)) { return (string.Empty, string.Empty); }

            string assemblyPath = Normalize(location);

            foreach (ContentPackage package in ContentPackageManager.EnabledPackages.All)
            {
                if (Declares(package, assemblyPath)) { return (package.ModVersion, package.Hash.StringRepresentation); }
            }

            foreach (ContentPackage package in ContentPackageManager.EnabledPackages.All)
            {
                string directory = Normalize(package.Dir);

                if (directory.Length == 0 || assemblyPath.Length <= directory.Length) { continue; }

                if (!assemblyPath.StartsWith(directory, StringComparison.OrdinalIgnoreCase)) { continue; }
                if (assemblyPath[directory.Length] != '/') { continue; }

                return (package.ModVersion, package.Hash.StringRepresentation);
            }
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Reading the sandbox menu content package failed: {e}");
        }

        return (string.Empty, string.Empty);
    }

    private static bool Declares(ContentPackage package, string assemblyPath)
    {
        foreach (PluginInfoFile info in package.AllFiles.OfType<PluginInfoFile>())
        {
            if (Declares(info.MainAssemblyInfos, assemblyPath) || Declares(info.ExtraAssemblyInfos, assemblyPath)) { return true; }
        }

        return false;
    }

    private static bool Declares(IEnumerable<PluginAssemblyInfo> assemblies, string assemblyPath)
    {
        foreach (PluginAssemblyInfo assembly in assemblies)
        {
            if (string.Equals(Normalize(assembly.AssemblyPath.FullPath), assemblyPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportUnknownIdentity()
    {
        if (_unknownIdentityReported) { return; }

        _unknownIdentityReported = true;

        Services.DebugConsole.AddWarning(
            "The sandbox menu could not read its own content package, so only the protocol version is checked.");
    }

    private static void ReportUnknownClient(string clientName)
    {
        if (_unknownClientReported) { return; }

        _unknownClientReported = true;

        Services.DebugConsole.AddWarning(
            $"{clientName} sent no sandbox menu version, so only the protocol version is checked. The client or the server probably cannot read its content package.");
    }

    private static string Shorten(string hash) => hash.Length > 7 ? hash[..7] : hash;

    private static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/');
}
