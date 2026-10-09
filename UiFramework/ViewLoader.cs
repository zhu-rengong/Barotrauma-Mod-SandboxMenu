namespace UiFramework;

public static class ViewLoader
{
    public static ViewContext Load(
        string fileName,
        object? dataContext,
        Action<ViewContext>? configure = null,
        Action<string>? diagnosticSink = null,
        RectTransform? parent = null)
    {
        MarkupNode root = MarkupSource.Load(fileName);
        return Build(root, fileName, dataContext, configure, diagnosticSink, parent);
    }

    public static Styling.ResourceDictionary LoadResources(string fileName)
        => Styling.ResourceDictionary.Read(MarkupSource.Load(fileName));

    internal static ViewContext Build(
        MarkupNode root,
        string view,
        object? dataContext,
        Action<ViewContext>? configure = null,
        Action<string>? diagnosticSink = null,
        RectTransform? parent = null)
    {
        ViewContext context = new(view) { DataContext = dataContext, DiagnosticSink = diagnosticSink };
        configure?.Invoke(context);
        context.Root = ViewBuilder.BuildElement(context, root, parent);
        context.Resources = context.Root?.Resources;
        context.Diagnostics.Flush(context.DiagnosticSink);
        return context;
    }
}
