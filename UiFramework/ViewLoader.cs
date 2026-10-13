using UiFramework.Animation;
using UiFramework.Styling;

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

    public static ResourceDictionary LoadResources(string fileName)
        => ResourceReader.Read(MarkupSource.Load(fileName));

    internal static ViewContext Build(
        MarkupNode root,
        string view,
        object? dataContext,
        Action<ViewContext>? configure = null,
        Action<string>? diagnosticSink = null,
        RectTransform? parent = null)
    {
        ViewContext context = new(view, new AnimationHost())
        {
            DataContext = dataContext,
            DiagnosticSink = diagnosticSink
        };

        try
        {
            configure?.Invoke(context);
            context.Root = ViewBuilder.BuildElement(context, root, parent);
            context.Resources = context.Root?.Resources;
        }
        catch (Exception e)
        {
            context.Diagnostics.Report($"the view could not be built ({e.Message})", root);
            context.Dispose();
        }

        context.Diagnostics.Flush(context.DiagnosticSink);

        return context;
    }
}
