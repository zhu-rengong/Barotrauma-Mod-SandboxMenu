using System.IO;
using System.Xml.Linq;
using SafeXML = Barotrauma.IO.SafeXML;

namespace SandboxMenu.Domain.Persistence;

internal static class TemplateStore
{
    private const string Extension = ".xml";

    private const int MaxNameLength = 96;

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private static readonly Dictionary<string, (long Stamp, SpawnSet? Set)> _shared = new(StringComparer.OrdinalIgnoreCase);

    public static string Folder => Path.Combine(Services.Settings.SaveFolder, "Presets");

    public static string PathOf(string name) => PathFor(name);

    public static bool Exists(string name) => File.Exists(PathFor(name));

    public static IReadOnlyList<string> ListPresets()
    {
        try
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
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Listing the presets failed: {e}");
            return [];
        }
    }

    public static bool TryLoad(string name, out SpawnSet? set)
    {
        try
        {
            string path = PathFor(name);

            set = File.Exists(path) && XMLExtensions.TryLoadXml(path) is { Root: { } root }
                ? SpawnSet.FromXml(root)
                : null;
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Failed to load preset '{name}': {e}");
            set = null;
        }

        return set is not null;
    }

    public static bool Save(SpawnSet set)
    {
        try
        {
            Directory.CreateDirectory(Folder);

            string path = PathFor(set.Name);
            DropOtherSpelling(path);

            SafeXML.SaveSafe(new XDocument(set.ToXml()), path, throwExceptions: true);
            return true;
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Failed to save preset '{set.Name}': {e}");
            return false;
        }
    }

    internal static bool TryLoadShared(string name, out SpawnSet? set)
    {
        long stamp = StampOf(name);

        if (_shared.TryGetValue(name, out (long Stamp, SpawnSet? Set) cached) && cached.Stamp == stamp)
        {
            set = cached.Set;
            return set is not null;
        }

        bool ok = TryLoad(name, out set);
        _shared[name] = (stamp, set);

        return ok;
    }

    private static string ReplaceInvalid(string name)
    {
        char[] chars = name.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(InvalidFileNameChars, chars[i]) >= 0) { chars[i] = '_'; }
        }

        return new string(chars);
    }

    private static long StampOf(string name)
    {
        try
        {
            string path = PathFor(name);

            return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
        }
        catch (Exception)
        {
            return 0L;
        }
    }

    internal static bool ReachesTemplate(SpawnSet set, string name)
        => Reaches(set.Entries, name, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static bool Reaches(IReadOnlyList<SpawnEntry> entries, string name, HashSet<string> visiting)
    {
        foreach (SpawnEntry entry in entries)
        {
            if (entry is not ReferenceEntry reference || visiting.Contains(reference.TemplateName)) { continue; }

            if (string.Equals(reference.TemplateName, name, StringComparison.OrdinalIgnoreCase)) { return true; }

            visiting.Add(reference.TemplateName);

            if (TryLoadShared(reference.TemplateName, out SpawnSet? referenced)
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
        string sanitized = (name.IndexOfAny(InvalidFileNameChars) < 0 ? name : ReplaceInvalid(name))
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
