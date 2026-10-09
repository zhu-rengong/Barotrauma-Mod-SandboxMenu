namespace UiFramework;

public interface IViewWindow
{
    GUIComponent Frame { get; }

    Point NominalSize { get; }
}
