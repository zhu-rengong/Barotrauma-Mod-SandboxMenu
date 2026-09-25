namespace UiFramework;

internal sealed class MarkupValue
{
    private MarkupValue(string raw, MarkupExtension? extension)
    {
        Raw = raw;
        Extension = extension;
    }

    internal string Raw { get; }

    internal MarkupExtension? Extension { get; }

    internal bool IsText => Extension is null;

    internal static MarkupValue Parse(string raw)
    {
        string trimmed = raw.Trim();

        if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[^1] != '}') { return new MarkupValue(trimmed, null); }
        if (trimmed.StartsWith("{}", StringComparison.Ordinal)) { return new MarkupValue(trimmed[2..], null); }

        return new MarkupValue(trimmed, MarkupExtension.Parse(trimmed));
    }
}
