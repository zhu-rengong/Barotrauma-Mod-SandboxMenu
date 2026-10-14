namespace UiFramework.Styling;

internal static class ResourceReader
{
    internal static ResourceDictionary Read(MarkupNode root, Action<string>? report = null)
        => Read(root, root.View, report, []);

    private static ResourceDictionary Read(MarkupNode root, string view, Action<string>? report, HashSet<string> merged)
    {
        ResourceDictionary dictionary = new();

        foreach (MarkupNode child in root.Children)
        {
            if (child.IsProperty && string.Equals(child.Name, "MergedDictionaries", StringComparison.OrdinalIgnoreCase))
            {
                foreach (MarkupNode source in child.Children) { MergeInto(dictionary, source, report, merged); }
                continue;
            }

            if (string.Equals(child.Name, "ResourceDictionary", StringComparison.OrdinalIgnoreCase))
            {
                MergeInto(dictionary, child, report, merged);
                continue;
            }

            if (string.Equals(child.Name, "Style", StringComparison.OrdinalIgnoreCase))
            {
                Style style = Style.Read(child, report);
                dictionary.Set(style.Key ?? style.TargetType ?? throw new InvalidDataException($"a style needs a Key or a TargetType ({child})"), style);
                continue;
            }

            if (string.Equals(child.Name, "Storyboard", StringComparison.OrdinalIgnoreCase))
            {
                Animation.Storyboard storyboard = Animation.AnimationMarkup.ReadStoryboard(child, report);
                dictionary.Set(storyboard.Key ?? throw new InvalidDataException($"a storyboard needs a Key ({child})"), storyboard);
                continue;
            }

            if (string.Equals(child.Name, "DataTemplate", StringComparison.OrdinalIgnoreCase))
            {
                Templating.DataTemplate template = Templating.DataTemplate.Read(child);
                dictionary.Set(template.Key ?? template.DataType ?? throw new InvalidDataException($"a data template needs a Key or a DataType ({child})"), template);
                continue;
            }

            report?.Invoke($"{view}: <{child.Name}> cannot be declared as a resource");
        }

        return dictionary;
    }

    private static void MergeInto(ResourceDictionary dictionary, MarkupNode source, Action<string>? report, HashSet<string> merged)
    {
        if (source.Text("Source") is not { Length: > 0 } file)
        {
            report?.Invoke($"{source.View}: a merged dictionary needs a Source");
            return;
        }

        if (!merged.Add(file)) { return; }

        ResourceDictionary read = Read(MarkupSource.Load(file), file, report, merged);

        foreach ((string key, object _) in read.Entries()) { dictionary.Set(key, read.Find(key)); }
    }
}
