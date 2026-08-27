using System.Windows.Input;

namespace Sockseek.Desktop;

internal sealed class DesktopCommand(Action execute) : ICommand
{
    private readonly Action execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}

internal sealed class DesktopAsyncCommand(Func<Task> executeAsync) : ICommand
{
    private readonly Func<Task> executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter) => await executeAsync();
}
