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

        RebuildNavigationItems();
        RebuildCommandPaletteItems();
    }

    private void HandleClosed(object? sender, EventArgs eventArgs)
    {
        if (viewModel is not null)
            viewModel.PropertyChanged -= HandleViewModelPropertyChanged;

        viewModel = null;
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(DesktopShellWindowViewModel.CurrentSection):
                RebuildNavigationItems();
                break;
            case nameof(DesktopShellWindowViewModel.IsCommandPaletteOpen):
                RebuildCommandPaletteItems();
                break;
            case nameof(DesktopShellWindowViewModel.CurrentTheme):
                if (viewModel is not null)
                    ApplyTheme(viewModel.CurrentTheme);
                break;
        }
    }

    private async void HandleStartDaemonClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (viewModel is null)
            return;

        await viewModel.TryStartDaemonAsync();
    }

    private async void HandleCopyDiagnosticsClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (viewModel is null)
            return;

        var diagnosticsText = viewModel.TryGetCopyDiagnosticsText();
        if (diagnosticsText is null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is not null)
            await topLevel.Clipboard.SetTextAsync(diagnosticsText);
    }

    private void HandleOpenCommandPaletteClick(object? sender, RoutedEventArgs eventArgs)
        => viewModel?.OpenCommandPalette();

    private void HandleCloseCommandPaletteClick(object? sender, RoutedEventArgs eventArgs)
        => viewModel?.CloseCommandPalette();

    private void HandleSystemThemeClick(object? sender, RoutedEventArgs eventArgs)
        => viewModel?.SetTheme(DesktopThemePreference.System);

    private void HandleLightThemeClick(object? sender, RoutedEventArgs eventArgs)
        => viewModel?.SetTheme(DesktopThemePreference.Light);

    private void HandleDarkThemeClick(object? sender, RoutedEventArgs eventArgs)
        => viewModel?.SetTheme(DesktopThemePreference.Dark);

    private void HandleNavigationClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (viewModel is null || sender is not Button { Tag: ShellSection section })
            return;

        viewModel.NavigateTo(section);
    }

    private void HandleCommandPaletteItemClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (viewModel is null || sender is not Button { Tag: string itemId })
            return;

        viewModel.TryExecuteCommandPaletteItem(itemId);
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

    private void RebuildNavigationItems()
    {
        var host = NavigationItemsHost;
        host.Children.Clear();

        if (viewModel is null)
            return;

        foreach (var item in viewModel.NavigationItems)
        {
            host.Children.Add(new Button
            {
                Content = item.Section == viewModel.CurrentSection
                    ? $"• {item.DisplayName} ({item.Shortcut})"
                    : $"{item.DisplayName} ({item.Shortcut})",
                Tag = item.Section,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            }.Also(button => button.Click += HandleNavigationClick));
        }
    }

    private void RebuildCommandPaletteItems()
    {
        var host = CommandPaletteItemsHost;
        host.Children.Clear();

        if (viewModel is null)
            return;

        foreach (var item in viewModel.CommandPalette.Items)
        {
            host.Children.Add(new Button
            {
                Content = string.IsNullOrWhiteSpace(item.Shortcut)
                    ? item.Title
                    : $"{item.Title} ({item.Shortcut})",
                Tag = item.Id,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            }.Also(button => button.Click += HandleCommandPaletteItemClick));
        }
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

internal static class ControlFactoryExtensions
{
    public static T Also<T>(this T value, Action<T> configure)
    {
        configure(value);
        return value;
    }
}
