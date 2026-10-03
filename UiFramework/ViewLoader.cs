namespace UiFramework;

// Turns a markup file into a loaded view: MarkupSource reads the file, ViewBuilder builds the tree.
internal static class ViewLoader
{
    internal static ViewContext Load(string fileName, object? dataContext, Action<string>? diagnosticSink = null, RectTransform? parent = null)
    {
        MarkupNode root = MarkupSource.Load(fileName);
        return Build(root, fileName, dataContext, diagnosticSink, parent);
    }

    internal static ViewContext Build(MarkupNode root, string view, object? dataContext, Action<string>? diagnosticSink = null, RectTransform? parent = null)
    {
        ViewContext context = new(view) { DataContext = dataContext, DiagnosticSink = diagnosticSink };
        context.Root = ViewBuilder.BuildElement(context, root, parent);
        context.Resources = context.Root?.Resources;
        context.Diagnostics.Flush(context.DiagnosticSink);
        return context;
    }
}
