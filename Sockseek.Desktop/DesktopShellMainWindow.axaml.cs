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
        if (viewModel is null)
            return;

        if (eventArgs.Key == Key.Escape && viewModel.IsCommandPaletteOpen)
        {
            viewModel.CloseCommandPalette();
            eventArgs.Handled = true;
            return;
        }

        if (!eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        if (TryMapShortcut(eventArgs.Key, out var shortcut) && viewModel.TryHandleShortcut(shortcut))
            eventArgs.Handled = true;
    }

    private static bool TryMapShortcut(Key key, out string shortcut)
    {
        shortcut = key switch
        {
            Key.D1 => "Ctrl+1",
            Key.L => "Ctrl+L",
            Key.D2 => "Ctrl+2",
            Key.D3 => "Ctrl+3",
            Key.D4 => "Ctrl+4",
            Key.D5 => "Ctrl+5",
            Key.OemComma => "Ctrl+,",
            Key.K => "Ctrl+K",
            _ => string.Empty,
        };

        return shortcut.Length > 0;
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
