using System.Windows.Input;
using UiFramework.Styling;

namespace UiFramework;

internal abstract class ViewElement(GUIComponent control)
{
    internal GUIComponent Control { get; } = control;

    internal MarkupNode? Node { get; set; }

    internal object? DataContext { get; set; }

    internal ResourceDictionary? Resources { get; set; }

    internal ViewElement? Parent { get; set; }

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

    internal virtual void AddContent(ViewElement child) => throw new NotSupportedException($"{GetType().Name} takes no content");

    internal virtual void ChildVisibilityChanged(ViewElement child)
    {
    }

    internal virtual RectTransform ContentParent => Control.RectTransform;

    public override string ToString() => GetType().Name;
}

internal interface IPropertyObserver
{
    void Observe(string property, Action<object?> changed);
}

internal interface ICommandElement
{
    void SetCommand(ICommand? command);

    void RefreshCanExecute();
}
