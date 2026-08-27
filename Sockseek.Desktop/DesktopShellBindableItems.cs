using System.Windows.Input;

namespace Sockseek.Desktop;

public sealed class DesktopShellNavigationItemViewModel : ObservableObject
{
    private bool isCurrent;

    public DesktopShellNavigationItemViewModel(
        ShellNavigationItem item,
        bool isCurrent,
        Action navigate)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        NavigateCommand = new DesktopCommand(navigate ?? throw new ArgumentNullException(nameof(navigate)));
        IsCurrent = isCurrent;
    }

    public ShellNavigationItem Item { get; }

    public ICommand NavigateCommand { get; }

    public bool IsCurrent
    {
        get => isCurrent;
        set
        {
            if (!SetProperty(ref isCurrent, value))
                return;

            OnPropertyChanged(nameof(DisplayLabel));
        }
    }

    public string DisplayLabel => IsCurrent
        ? $"• {Item.DisplayName} ({Item.Shortcut})"
        : $"{Item.DisplayName} ({Item.Shortcut})";
}

public sealed class DesktopShellCommandPaletteItemViewModel
{
    public DesktopShellCommandPaletteItemViewModel(
        CommandPaletteItemViewModel item,
        Action execute)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        ExecuteCommand = new DesktopCommand(execute ?? throw new ArgumentNullException(nameof(execute)));
    }

    public CommandPaletteItemViewModel Item { get; }

    public ICommand ExecuteCommand { get; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(Item.Shortcut)
        ? Item.Title
        : $"{Item.Title} ({Item.Shortcut})";
}
