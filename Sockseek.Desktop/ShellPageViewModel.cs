namespace Sockseek.Desktop;

public sealed class ShellPageViewModel
{
    public ShellPageViewModel(
        ShellSection section,
        string title,
        string description,
        string titleResourceKey,
        string descriptionResourceKey,
        string iconToken)
    {
        Section = section;
        Title = title;
        Description = description;
        TitleResourceKey = titleResourceKey;
        DescriptionResourceKey = descriptionResourceKey;
        IconToken = iconToken;
    }

    public ShellSection Section { get; }

    public string Title { get; }

    public string Description { get; }

    public string TitleResourceKey { get; }

    public string DescriptionResourceKey { get; }

    public string IconToken { get; }

    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.Page;

    public string TitleTypographyToken { get; } = DesktopDesignTokens.Typography.PageTitle;

    public string DescriptionTypographyToken { get; } = DesktopDesignTokens.Typography.Body;

    public string ContentSpacingToken { get; } = DesktopDesignTokens.Spacing.PageContent;
}
