namespace Sockseek.Desktop;

public sealed class BackendStatusBannerViewModel
{
    public BackendStatusBannerViewModel(
        BackendConnectionState state,
        string title,
        string message,
        bool isVisible,
        string surfaceToken,
        string iconToken,
        string titleResourceKey,
        string messageResourceKey)
    {
        State = state;
        Title = title;
        Message = message;
        IsVisible = isVisible;
        SurfaceToken = surfaceToken;
        IconToken = iconToken;
        TitleResourceKey = titleResourceKey;
        MessageResourceKey = messageResourceKey;
    }

    public BackendConnectionState State { get; }

    public string Title { get; }

    public string Message { get; }

    public bool IsVisible { get; }

    public string SurfaceToken { get; }

    public string IconToken { get; }

    public string TitleResourceKey { get; }

    public string MessageResourceKey { get; }

    public string TitleTypographyToken { get; } = DesktopDesignTokens.Typography.BannerTitle;

    public string MessageTypographyToken { get; } = DesktopDesignTokens.Typography.BannerMessage;

    public string PaddingToken { get; } = DesktopDesignTokens.Spacing.BannerPadding;
}
