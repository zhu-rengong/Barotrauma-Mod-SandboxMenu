using System.Windows.Input;

namespace UiFramework;

// A command the framework itself offers as an action, so markup can name a behaviour the way it names a binding.
internal sealed class ViewCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
