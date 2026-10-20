namespace Sockseek.Infrastructure.SoulseekSharing;

public static class SoulseekRemotePath
{
    public const int MaximumLength = 4096;

    public static string Root(string virtualName) => $"@@{virtualName}";

    public static string Combine(string virtualName, string relativePath)
        => $"{Root(virtualName)}\\{relativePath.Replace('/', '\\')}";

    public static bool TryNormalize(string? remotePath, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(remotePath)
            || remotePath.Length > MaximumLength
            || remotePath.Any(char.IsControl))
        {
            return false;
        }

        string candidate = remotePath.Replace('/', '\\');
        string[] segments = candidate.Split('\\');
        if (segments.Length < 2
            || !segments[0].StartsWith("@@", StringComparison.Ordinal)
            || segments[0].Length == 2
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            return false;
        }

        normalized = string.Join('\\', segments);
        return true;
    }
}
