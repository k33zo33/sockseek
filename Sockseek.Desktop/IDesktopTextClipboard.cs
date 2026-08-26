namespace Sockseek.Desktop;

public interface IDesktopTextClipboard
{
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}
