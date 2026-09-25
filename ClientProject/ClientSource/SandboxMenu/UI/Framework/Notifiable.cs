using System.ComponentModel;
using System.Runtime.CompilerServices;
// ICommand lives in System.ObjectModel.dll — part of the shared .NET 8 base library on every platform. The
// namespace is named "Windows", but it is not WPF and keeps the mod cross-platform (see rule 10).
using System.Windows.Input;

namespace SandboxMenu.UI.Framework;

public abstract class Notifiable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) { return false; }

        field = value;
        Raise(property);
        return true;
    }

    protected void Raise([CallerMemberName] string? property = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
