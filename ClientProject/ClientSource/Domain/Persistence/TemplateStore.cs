using System.IO;
using System.Xml.Linq;
using SafeXML = Barotrauma.IO.SafeXML;

namespace SandboxMenu.Domain.Persistence;

internal static class TemplateStore
{
    private const string Extension = ".xml";

    private const int MaxNameLength = 96;

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private static readonly Dictionary<string, SpawnSet?> _shared = new(StringComparer.OrdinalIgnoreCase);

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
            Services.DebugConsole.AddWarning($"Listing the presets failed: {e}");
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
            Services.DebugConsole.AddWarning($"Failed to load preset '{name}': {e}");
            set = null;
        }

        return set is not null;
    }

    public static string? ReadText(string name)
    {
        string path = PathFor(name);

        if (!File.Exists(path)) { return null; }

        try
        {
            return Format(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Failed to read preset '{name}': {e}");
            return null;
        }
    }

    public static bool TryParse(string name, string text, out SpawnSet? set)
    {
        try
        {
            if (XDocument.Parse(text).Root is not { } root) { set = null; return false; }
            if (!string.Equals(root.Name.LocalName, "SpawnSet", StringComparison.Ordinal)) { set = null; return false; }

            set = SpawnSet.FromXml(root);
            set.Name = name;

            return true;
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"The text given for preset '{name}' is not valid: {e}");
            set = null;
            return false;
        }
    }

    public static bool WriteText(string name, string text)
    {
        if (!TryParse(name, text, out SpawnSet? set) || set is null) { return false; }

        try
        {
            Directory.CreateDirectory(Folder);

            string path = PathFor(name);
            DropOtherSpelling(path);

            File.WriteAllText(path, Normalize(text));

            _shared[name] = set;

            return true;
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Failed to save preset '{name}': {e}");
            return false;
        }
    }

    public static bool Save(SpawnSet set)
    {
        try
        {
            Directory.CreateDirectory(Folder);

            string path = PathFor(set.Name);
            DropOtherSpelling(path);

            SafeXML.SaveSafe(new XDocument(set.ToXml()), path, throwExceptions: true);

            _shared[set.Name] = set;

            return true;
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Failed to save preset '{set.Name}': {e}");
            return false;
        }
    }

    internal static bool TryLoadShared(string name, out SpawnSet? set)
    {
        if (_shared.TryGetValue(name, out set)) { return set is not null; }

        TryLoad(name, out set);
        _shared[name] = set;

        return set is not null;
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

    internal static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string Format(string text)
    {
        try
        {
            return Normalize(XDocument.Parse(text).ToString());
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Preset text is not valid XML and is shown as-is: {e.Message}");
            return Normalize(text);
        }
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
