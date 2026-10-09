using System.Windows.Input;
using UiFramework.Styling;

namespace UiFramework;

public abstract class ViewElement(GUIComponent control)
{
    public GUIComponent Control { get; } = control;

    internal MarkupNode? Node { get; set; }

    public ViewContext? Context { get; internal set; }

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
