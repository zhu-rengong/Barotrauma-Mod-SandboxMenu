using System.Windows.Input;
using UiFramework.Styling;

namespace UiFramework;

public abstract class ViewElement(GUIComponent control)
{
    public GUIComponent Control { get; } = control;

    internal MarkupNode? Node { get; set; }

    // The view the element was built in: what a behaviour offered by markup acts through.
    public ViewContext? Context { get; internal set; }

    // The keys this element answers to, and the element that decides which of them are live: a key declared inside
    // a hidden area stands back with it.
    internal List<Input.KeyBinding>? Bindings { get; set; }

    public object? DataContext { get; set; }

    public ResourceDictionary? Resources { get; set; }

    public ViewElement? Parent { get; set; }

    [ElementProperty]
    public bool Visible
    {
        set
        {
            if (Control.Visible == value) { return; }

            Control.Visible = value;
            Parent?.ChildVisibilityChanged(this);
        }
    }

    // Marks a region as the window's drag handle: the shell is what knows how the host moves a window.
    [ElementProperty]
    public bool Drag
    {
        set
        {
            if (value) { Context?.AttachDrag(Control.RectTransform); }
        }
    }

    public virtual void AddContent(ViewElement child) => throw new NotSupportedException($"{GetType().Name} takes no content");

    public virtual void ChildVisibilityChanged(ViewElement child)
    {
    }

    public virtual RectTransform ContentParent => Control.RectTransform;

    public override string ToString() => GetType().Name;
}

public interface IPropertyObserver
{
    void Observe(string property, Action<object?> changed);
}

public interface ICommandElement
{
    void SetCommand(ICommand? command);

    void RefreshCanExecute();
}
