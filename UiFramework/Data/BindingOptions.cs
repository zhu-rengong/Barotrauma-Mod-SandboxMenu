namespace UiFramework.Data;

internal sealed class BindingOptions
{
    internal string Path { get; private init; } = string.Empty;

    internal BindingMode? Mode { get; private init; }

    internal string? Converter { get; private init; }

    internal string? ConverterParameter { get; private init; }

    internal string? Format { get; private init; }

    internal string? Fallback { get; private init; }

    internal string? NullValue { get; private init; }

    internal string? ElementName { get; private init; }

    internal bool Self { get; private init; }

    internal static BindingOptions Read(MarkupNode node) => new()
    {
        Path = node.Text("Path") ?? string.Empty,
        Mode = ParseMode(node.Text("Mode")),
        Converter = node.Text("Converter"),
        ConverterParameter = node.Text("ConverterParameter"),
        Format = node.Text("StringFormat"),
        Fallback = node.Text("FallbackValue"),
        NullValue = node.Text("TargetNullValue"),
        ElementName = node.Text("ElementName"),
        Self = string.Equals(node.Text("RelativeSource"), "Self", StringComparison.OrdinalIgnoreCase)
    };

    internal static BindingOptions Parse(string body)
    {
        string[] parts = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0) { return new BindingOptions(); }

        string head = parts[0];
        int space = head.IndexOf(' ', StringComparison.Ordinal);
        string path = space < 0 ? string.Empty : head[(space + 1)..].Trim();

        string? mode = null;
        string? converter = null;
        string? parameter = null;
        string? format = null;
        string? fallback = null;
        string? nullValue = null;
        string? elementName = null;
        bool self = false;

        foreach (string option in parts.Skip(1))
        {
            int equals = option.IndexOf('=', StringComparison.Ordinal);
            string name = (equals < 0 ? option : option[..equals]).Trim();
            string text = equals < 0 ? string.Empty : option[(equals + 1)..].Trim();

            if (name.Equals("Mode", StringComparison.OrdinalIgnoreCase)) { mode = text; }
            else if (name.Equals("Converter", StringComparison.OrdinalIgnoreCase)) { converter = text; }
            else if (name.Equals("ConverterParameter", StringComparison.OrdinalIgnoreCase)) { parameter = text; }
            else if (name.Equals("StringFormat", StringComparison.OrdinalIgnoreCase)) { format = text; }
            else if (name.Equals("FallbackValue", StringComparison.OrdinalIgnoreCase)) { fallback = text; }
            else if (name.Equals("TargetNullValue", StringComparison.OrdinalIgnoreCase)) { nullValue = text; }
            else if (name.Equals("ElementName", StringComparison.OrdinalIgnoreCase)) { elementName = text; }
            else if (name.Equals("RelativeSource", StringComparison.OrdinalIgnoreCase)) { self = text.Equals("Self", StringComparison.OrdinalIgnoreCase); }
        }

        return new BindingOptions
        {
            Path = path,
            Mode = ParseMode(mode),
            Converter = converter,
            ConverterParameter = parameter,
            Format = format,
            Fallback = fallback,
            NullValue = nullValue,
            ElementName = elementName,
            Self = self
        };
    }

    private static BindingMode? ParseMode(string? mode) => mode switch
    {
        null => null,
        _ when mode.Contains("TwoWay", StringComparison.OrdinalIgnoreCase) => BindingMode.TwoWay,
        _ when mode.Contains("OneTime", StringComparison.OrdinalIgnoreCase) => BindingMode.OneTime,
        _ when mode.Contains("OneWayToSource", StringComparison.OrdinalIgnoreCase) => BindingMode.OneWayToSource,
        _ when mode.Contains("OneWay", StringComparison.OrdinalIgnoreCase) => BindingMode.OneWay,
        _ => null
    };
}
