namespace Sockseek.Desktop;

public static class DesktopDaemonStartupParser
{
    public const string HandshakePrefix = "SOCKSEEK_DAEMON_HANDSHAKE=";

    public static bool TryParseHandshakeLine(string line, out DesktopDaemonHandshake? handshake)
    {
        handshake = null;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        int markerIndex = line.IndexOf(HandshakePrefix, StringComparison.Ordinal);
        if (markerIndex < 0)
            return false;

        string payload = line[(markerIndex + HandshakePrefix.Length)..].Trim();
        return DesktopDaemonHandshake.TryParse(payload, out handshake);
    }

    public static async Task<DesktopDaemonHandshake?> WaitForHandshakeAsync(
        IAsyncEnumerable<string> outputLines,
        CancellationToken cancellationToken = default)
    {
        await foreach (var line in outputLines.WithCancellation(cancellationToken))
        {
            if (TryParseHandshakeLine(line, out var handshake) && handshake is not null)
                return handshake;
        }

        return null;
    }
}
