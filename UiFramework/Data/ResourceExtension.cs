namespace UiFramework.Data;

internal sealed class StaticResourceExtension(string key) : MarkupExtension
{
    private readonly string _key = key;

    internal static MarkupExtension Read(string body) => new StaticResourceExtension(body.Split(' ', 2, StringSplitOptions.TrimEntries)[^1]);

    internal override void Apply(PropertyAssignment assignment) => assignment.SetResource(_key, reevaluate: false);
}

internal sealed class DynamicResourceExtension(string key) : MarkupExtension
{
    private readonly string _key = key;

    internal static MarkupExtension Read(string body) => new DynamicResourceExtension(body.Split(' ', 2, StringSplitOptions.TrimEntries)[^1]);

    internal override void Apply(PropertyAssignment assignment) => assignment.SetResource(_key, reevaluate: true);
}
