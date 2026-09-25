using System.IO;
using System.Xml.Linq;
// The host's own saving entry point, taken by name: importing Barotrauma.IO would clash with System.IO over the
// Path, File and Directory this file uses to keep the mod's presets together.
using SafeXML = Barotrauma.IO.SafeXML;

namespace SandboxMenu.Domain.Persistence;

public static class TemplateStore
{
    private const string Extension = ".xml";

    public static string Folder => Path.Combine(Plugin.SettingsService.SaveFolder, "Presets");

    public static string PathOf(string name) => PathFor(name);

    public static bool Exists(string name) => File.Exists(PathFor(name));

    public static IReadOnlyList<string> ListPresets()
        => Attempt<IReadOnlyList<string>>(
            static () =>
            {
                if (!Directory.Exists(Folder)) { return []; }

                return
                [
                    .. Directory.EnumerateFiles(Folder, "*" + Extension)
                        .Select(Path.GetFileNameWithoutExtension)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .Select(name => name!)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                ];
            },
            "Failed to list presets",
            []);

    public static bool TryLoad(string name, out SpawnSet? set)
    {
        set = Attempt<SpawnSet?>(
            () =>
            {
                string path = PathFor(name);
                if (!File.Exists(path)) { return null; }

                // The host's own loader, which reads with the same hardened settings it uses for content XML and is
                // the one place that reports a file it cannot read.
                return XMLExtensions.TryLoadXml(path) is { Root: { } root } ? SpawnSet.FromXml(root) : null;
            },
            $"Failed to load preset '{name}'",
            null);

        return set is not null;
    }

    public static bool Save(SpawnSet set)
        => Attempt<bool>(
            () =>
            {
                Directory.CreateDirectory(Folder);

                string path = PathFor(set.Name);
                DropOtherSpelling(path);

                // The host's own save path, which is also what decides whether this folder may be written at all.
                // It is asked to throw, so that a refusal reaches the status line instead of only the log.
                SafeXML.SaveSafe(new XDocument(set.ToXml()), path, throwExceptions: true);
                return true;
            },
            $"Failed to save preset '{set.Name}'",
            false);

    internal static bool ReachesTemplate(SpawnSet set, string name)
        => Reaches(set.Entries, name, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static bool Reaches(IReadOnlyList<SpawnEntry> entries, string name, HashSet<string> visiting)
    {
        foreach (SpawnEntry entry in entries)
        {
            if (entry is not RefEntry reference || visiting.Contains(reference.TemplateName)) { continue; }

            if (string.Equals(reference.TemplateName, name, StringComparison.OrdinalIgnoreCase)) { return true; }

            visiting.Add(reference.TemplateName);

            if (TryLoad(reference.TemplateName, out SpawnSet? referenced)
                && referenced is not null
                && Reaches(referenced.Entries, name, visiting))
            {
                return true;
            }
        }

        return false;
    }

    public static bool Delete(string name)
        => Attempt<bool>(
            () =>
            {
                string path = PathFor(name);
                if (!File.Exists(path)) { return false; }

                File.Delete(path);
                return true;
            },
            $"Failed to delete preset '{name}'",
            false);

    private static T Attempt<T>(Func<T> body, string message, T fallback) => Guard.Try(message, body, fallback);

    private static string PathFor(string name) => Path.Combine(Folder, Sanitize(name) + Extension);

    private static void DropOtherSpelling(string path)
    {
        string name = Path.GetFileName(path);

        foreach (string existing in Directory.EnumerateFiles(Folder, "*" + Extension))
        {
            if (string.Equals(existing, path, StringComparison.Ordinal)) { continue; }
            if (!string.Equals(Path.GetFileName(existing), name, StringComparison.OrdinalIgnoreCase)) { continue; }

            File.Delete(existing);
        }
    }

    private static string Sanitize(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();

        string sanitized = new string([.. name.Select(c => invalid.Contains(c) ? '_' : c)])
            .Trim()
            .TrimEnd('.');

        if (sanitized.Length > 96) { sanitized = sanitized[..96]; }

        return string.IsNullOrEmpty(sanitized) ? "preset" : sanitized;
    }
}
