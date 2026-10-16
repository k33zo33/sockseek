using System.Diagnostics;
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

    public async void Execute(object? parameter) => await ExecuteAsync();

    internal async Task ExecuteAsync()
    {
        try
        {
            await executeAsync();
        }
        catch (Exception exception) when (IsRecoverableTransportFailure(exception))
        {
            Trace.TraceError("Desktop command failed because the local daemon was unavailable: {0}", exception);
        }
    }

    private static bool IsRecoverableTransportFailure(Exception exception)
        => exception is HttpRequestException or OperationCanceledException;
}

internal sealed class DesktopAsyncParameterCommand<T>(Func<T, Task> executeAsync) : ICommand
{
    private readonly Func<T, Task> executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => parameter is T;

    public async void Execute(object? parameter)
    {
        if (parameter is T typedParameter)
            await ExecuteAsync(typedParameter);
    }

    internal async Task ExecuteAsync(T parameter)
    {
        try
        {
            await executeAsync(parameter);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Trace.TraceError("Desktop command failed because the local daemon was unavailable: {0}", exception);
        }
    }
}
