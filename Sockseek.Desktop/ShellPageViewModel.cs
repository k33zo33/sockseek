namespace Sockseek.Desktop;

public sealed class ShellPageViewModel
{
    public ShellPageViewModel(
        ShellSection section,
        string title,
        string description,
        string titleResourceKey,
        string descriptionResourceKey,
        string iconToken,
        string badgeLabel,
        string emptyStateTitle,
        string emptyStateDescription,
        string emptyStateTitleResourceKey,
        string emptyStateDescriptionResourceKey,
        IReadOnlyList<ShellPageDetailItemViewModel> highlights)
    {
        Section = section;
        Title = title;
        Description = description;
        TitleResourceKey = titleResourceKey;
        DescriptionResourceKey = descriptionResourceKey;
        IconToken = iconToken;
        BadgeLabel = badgeLabel;
        EmptyStateTitle = emptyStateTitle;
        EmptyStateDescription = emptyStateDescription;
        EmptyStateTitleResourceKey = emptyStateTitleResourceKey;
        EmptyStateDescriptionResourceKey = emptyStateDescriptionResourceKey;
        Highlights = highlights ?? throw new ArgumentNullException(nameof(highlights));
    }

    public ShellSection Section { get; }

    public string Title { get; }

    public string Description { get; }

    public string TitleResourceKey { get; }

    public string DescriptionResourceKey { get; }

    public string IconToken { get; }

    public string BadgeLabel { get; }

    public string EmptyStateTitle { get; }

    public string EmptyStateDescription { get; }

    public string EmptyStateTitleResourceKey { get; }

    public string EmptyStateDescriptionResourceKey { get; }

    public IReadOnlyList<ShellPageDetailItemViewModel> Highlights { get; }

    public string SurfaceToken { get; } = DesktopDesignTokens.Surface.Page;

    public string TitleTypographyToken { get; } = DesktopDesignTokens.Typography.PageTitle;

    public string DescriptionTypographyToken { get; } = DesktopDesignTokens.Typography.Body;

    public string ContentSpacingToken { get; } = DesktopDesignTokens.Spacing.PageContent;
}
