namespace UiFramework.Styling;

internal static class DefaultTheme
{
    private static ResourceDictionary? _theme;

    static DefaultTheme() => ModLifetime.Unloading += () => _theme = null;

    internal static ResourceDictionary Theme => _theme ??= Build();

    private static ResourceDictionary Build()
    {
        ResourceDictionary theme = new();

        theme.Set("SectionHeader", new Style
        {
            Key = "SectionHeader",
            TargetType = "Text",
            Setters = [new Setter("FontSize", MarkupValue.Parse("24"))]
        });

        theme.Set("Text", new Style
        {
            TargetType = "Text",
            Setters =
            [
                new Setter("FontSize", MarkupValue.Parse("22")),
                new Setter("Wrap", MarkupValue.Parse("True")),
                new Setter("Scale", MarkupValue.Parse("False"))
            ]
        });

        return theme;
    }
}
