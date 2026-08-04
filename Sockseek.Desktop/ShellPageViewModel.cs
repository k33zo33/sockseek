namespace Sockseek.Desktop;

public sealed class ShellPageViewModel
{
    public ShellPageViewModel(ShellSection section, string title, string description)
    {
        Section = section;
        Title = title;
        Description = description;
    }

    public ShellSection Section { get; }

    public string Title { get; }

    public string Description { get; }
}
