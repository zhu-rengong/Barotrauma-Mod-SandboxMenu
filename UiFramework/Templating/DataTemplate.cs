namespace UiFramework.Templating;

internal sealed class DataTemplate
{
    internal string? Key { get; private init; }

    internal string? DataType { get; private init; }

    internal MarkupNode Content { get; private init; } = null!;

    internal static DataTemplate Read(MarkupNode node)
    {
        MarkupNode? content = node.Content.FirstOrDefault()
            ?? throw new InvalidDataException($"<{node.Name}> needs exactly one element ({node})");

        return new DataTemplate { Key = node.Text("Key"), DataType = node.Text("DataType"), Content = content };
    }

    internal ViewElement Build(ViewLoadContext view, object? item, RectTransform parent, ViewElement? parentElement = null)
        => ViewLoader.BuildElement(view, Content, parent, item, parentElement);

    internal static DataTemplate? Select(object? item, string? explicitKey, ViewLoadContext view, ViewElement element)
    {
        if (explicitKey is { } key)
        {
            return view.FindResource(key, element) as DataTemplate;
        }

        if (item is null) { return null; }

        for (Type? type = item.GetType(); type is not null && type != typeof(object); type = type.BaseType)
        {
            if (view.FindResource(type.Name, element) is DataTemplate template) { return template; }
        }

        return null;
    }
}
