namespace Sockseek.Desktop;

public sealed record DesktopShellDiagnosticsSnapshot(
    string WindowTitle,
    string CurrentPageTitle,
    string Theme,
    string BackendState,
    string BackendBannerTitle,
    bool HasHandshake,
    string? BackendBaseUrl)
{
    public string ToDisplayText()
        => string.Join(
            Environment.NewLine,
            [
                $"Window: {WindowTitle}",
                $"Page: {CurrentPageTitle}",
                $"Theme: {Theme}",
                $"Backend state: {BackendState}",
                $"Backend banner: {BackendBannerTitle}",
                $"Handshake present: {HasHandshake}",
                $"Backend URL: {BackendBaseUrl ?? "unavailable"}"
            ]);
}
