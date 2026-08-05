namespace Sockseek.Desktop;

public sealed class ShellPageViewModel
{
    public ShellPageViewModel(
        ShellSection section,
        string title,
        string description,
        string titleResourceKey,
        string descriptionResourceKey)
    {
        Section = section;
        Title = title;
        Description = description;
        TitleResourceKey = titleResourceKey;
        DescriptionResourceKey = descriptionResourceKey;
    }

    public ShellSection Section { get; }

    public string Title { get; }

    public string Description { get; }

    public string TitleResourceKey { get; }

    public string DescriptionResourceKey { get; }
}
