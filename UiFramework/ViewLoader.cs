namespace UiFramework;

// Turns a markup file into a loaded view: MarkupSource reads the file, ViewBuilder builds the tree. The views live
// in the mod's assembly, so the mod names it once through UiHost.RegisterViewAssembly.
public static class ViewLoader
{
    // configure runs before the tree is built: it is where the shell hands the view what markup cannot know, such as
    // what closing it means or how a region becomes a drag handle.
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

    // A dictionary on its own, for the styles and templates a view falls back on rather than declares.
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
