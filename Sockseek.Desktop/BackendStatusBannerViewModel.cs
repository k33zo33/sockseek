namespace Sockseek.Desktop;

public sealed class BackendStatusBannerViewModel
{
    public BackendStatusBannerViewModel(BackendConnectionState state, string title, string message, bool isVisible)
    {
        State = state;
        Title = title;
        Message = message;
        IsVisible = isVisible;
    }

    public BackendConnectionState State { get; }

    public string Title { get; }

    public string Message { get; }

    public bool IsVisible { get; }
}
