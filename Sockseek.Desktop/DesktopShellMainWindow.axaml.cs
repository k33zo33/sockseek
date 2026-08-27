using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Sockseek.Desktop;

public partial class DesktopShellMainWindow : Window
{
    private DesktopShellWindowViewModel? viewModel;

    public DesktopShellMainWindow()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChanged;
        KeyDown += HandleKeyDown;
        Closed += HandleClosed;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void HandleDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (viewModel is not null)
            viewModel.PropertyChanged -= HandleViewModelPropertyChanged;

        viewModel = DataContext as DesktopShellWindowViewModel;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += HandleViewModelPropertyChanged;
            ApplyTheme(viewModel.CurrentTheme);
        }
    }

    private void HandleClosed(object? sender, EventArgs eventArgs)
    {
        if (viewModel is not null)
            viewModel.PropertyChanged -= HandleViewModelPropertyChanged;

        viewModel = null;
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(DesktopShellWindowViewModel.CurrentTheme) && viewModel is not null)
            ApplyTheme(viewModel.CurrentTheme);
    }

    private async void HandleCopyDiagnosticsClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (viewModel is null)
            return;

        await viewModel.TryCopyDiagnosticsAsync(new AvaloniaWindowTextClipboard(this));
    }

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (DesktopShellKeyRouting.TryHandleKeyGesture(viewModel, eventArgs.Key, eventArgs.KeyModifiers))
            eventArgs.Handled = true;
    }

    private static void ApplyTheme(DesktopThemePreference preference)
    {
        if (Application.Current is App app)
            app.ApplyThemePreference(preference);
    }
}

internal sealed class AvaloniaWindowTextClipboard(Window owner) : IDesktopTextClipboard
{
    private readonly Window owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.Clipboard is null)
            return;

        await topLevel.Clipboard.SetTextAsync(text);
    }
}
