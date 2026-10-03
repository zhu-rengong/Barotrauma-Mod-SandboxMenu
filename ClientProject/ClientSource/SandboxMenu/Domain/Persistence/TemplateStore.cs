using System.IO;
using System.Xml.Linq;
using SafeXML = Barotrauma.IO.SafeXML;

namespace SandboxMenu.Domain.Persistence;

internal static class TemplateStore
{
    private const string Extension = ".xml";

    private const int MaxNameLength = 96;

    public static string Folder => Path.Combine(Plugin.SettingsService.SaveFolder, "Presets");

    public static string PathOf(string name) => PathFor(name);

    public static bool Exists(string name) => File.Exists(PathFor(name));

    public static IReadOnlyList<string> ListPresets()
        => Guard.Try<IReadOnlyList<string>>(
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
            []);

    public static bool TryLoad(string name, out SpawnSet? set)
    {
        set = Guard.Try<SpawnSet?>(
            () =>
            {
                string path = PathFor(name);
                if (!File.Exists(path)) { return null; }

                return XMLExtensions.TryLoadXml(path) is { Root: { } root } ? SpawnSet.FromXml(root) : null;
            },
            null,
            $"Failed to load preset '{name}'");

        return set is not null;
    }

    public static bool Save(SpawnSet set)
        => Guard.Try<bool>(
            () =>
            {
                Directory.CreateDirectory(Folder);

                string path = PathFor(set.Name);
                DropOtherSpelling(path);

                SafeXML.SaveSafe(new XDocument(set.ToXml()), path, throwExceptions: true);
                return true;
            },
            false,
            $"Failed to save preset '{set.Name}'");

    internal static bool ReachesTemplate(SpawnSet set, string name)
        => Reaches(set.Entries, name, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static bool Reaches(IReadOnlyList<SpawnEntry> entries, string name, HashSet<string> visiting)
    {
        foreach (SpawnEntry entry in entries)
        {
            if (entry is not ReferenceEntry reference || visiting.Contains(reference.TemplateName)) { continue; }

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

    private static string PathFor(string name) => Path.Combine(Folder, Sanitize(name) + Extension);

    private static void DropOtherSpelling(string path)
    {
        if (!IsCaseInsensitive(Folder)) { return; }

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

        if (sanitized.Length > MaxNameLength) { sanitized = sanitized[..MaxNameLength]; }

        return string.IsNullOrEmpty(sanitized) ? "preset" : sanitized;
    }

    private static bool IsCaseInsensitive(string folder)
    {
        string parent = Path.GetDirectoryName(folder) ?? folder;
        string name = Path.GetFileName(folder);

        return Flipped(name) is { } flipped && Directory.Exists(Path.Combine(parent, flipped));
    }

    private static string? Flipped(string name)
    {
        string flipped = new([.. name.Select(c => char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c))]);

        return string.Equals(flipped, name, StringComparison.Ordinal) ? null : flipped;
    }
}
