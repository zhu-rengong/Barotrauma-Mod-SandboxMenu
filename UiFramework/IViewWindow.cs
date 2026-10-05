namespace UiFramework;

// What a shell holds on to once a view has been built: the frame it puts in the update list, and the size the markup
// asked for, which it keeps the window at when the resolution moves.
public interface IViewWindow
{
    GUIComponent Frame { get; }

    Point NominalSize { get; }
}
