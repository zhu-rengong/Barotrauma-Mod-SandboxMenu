namespace UiFramework.Styling;

internal static class GuiStyles
{
    internal static GUIComponentStyle? Resolve(string? name, Action<string>? report = null)
    {
        if (name is not { Length: > 0 }) { return null; }

        string text = name.Trim();
        int dot = text.IndexOf('.', StringComparison.Ordinal);

        if (dot < 0) { return Root(text, report); }

        string owner = text[..dot];
        GUIComponentStyle? parent = Root(owner, report);

        if (parent is null) { return null; }

        string child = text[(dot + 1)..];

        if (parent.ChildStyles.TryGetValue(child.ToIdentifier(), out GUIComponentStyle? style)) { return style; }

        report?.Invoke($"the GUI style '{owner}' has no child style '{child}'");

        return null;
    }

    private static GUIComponentStyle? Root(string name, Action<string>? report)
    {
        GUIComponentStyle? style = GUIStyle.GetComponentStyle(name);

        if (style is null) { report?.Invoke($"no GUI style '{name}' is loaded"); }

        return style;
    }
}
