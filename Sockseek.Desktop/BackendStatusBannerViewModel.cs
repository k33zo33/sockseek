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
        string messageResourceKey,
        string iconAccessibilityLabel,
        string iconAccessibilityLabelResourceKey,
        bool canCopyDiagnostics,
        string? copyDiagnosticsLabel,
        string? copyDiagnosticsLabelResourceKey,
        string? copyDiagnosticsHint,
        string? copyDiagnosticsHintResourceKey)
    {
        State = state;
        Title = title;
        Message = message;
        IsVisible = isVisible;
        SurfaceToken = surfaceToken;
        IconToken = iconToken;
        TitleResourceKey = titleResourceKey;
        MessageResourceKey = messageResourceKey;
        IconAccessibilityLabel = iconAccessibilityLabel;
        IconAccessibilityLabelResourceKey = iconAccessibilityLabelResourceKey;
        CanCopyDiagnostics = canCopyDiagnostics;
        CopyDiagnosticsLabel = copyDiagnosticsLabel;
        CopyDiagnosticsLabelResourceKey = copyDiagnosticsLabelResourceKey;
        CopyDiagnosticsHint = copyDiagnosticsHint;
        CopyDiagnosticsHintResourceKey = copyDiagnosticsHintResourceKey;
    }

    public BackendConnectionState State { get; }

    public string Title { get; }

    public string Message { get; }

    public bool IsVisible { get; }

    public string SurfaceToken { get; }

    public string IconToken { get; }

    public string TitleResourceKey { get; }

    public string MessageResourceKey { get; }

    public string IconAccessibilityLabel { get; }

    public string IconAccessibilityLabelResourceKey { get; }

    public bool CanCopyDiagnostics { get; }

    public string? CopyDiagnosticsLabel { get; }

    public string? CopyDiagnosticsLabelResourceKey { get; }

    public string? CopyDiagnosticsHint { get; }

    public string? CopyDiagnosticsHintResourceKey { get; }

    public string TitleTypographyToken { get; } = DesktopDesignTokens.Typography.BannerTitle;

    public string MessageTypographyToken { get; } = DesktopDesignTokens.Typography.BannerMessage;

    public string PaddingToken { get; } = DesktopDesignTokens.Spacing.BannerPadding;
}
