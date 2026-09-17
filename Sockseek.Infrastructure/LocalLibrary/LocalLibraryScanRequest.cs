namespace Sockseek.Infrastructure.LocalLibrary;

public sealed record LocalLibraryScanRequest(
    IReadOnlyList<string> Roots,
    IReadOnlySet<string>? SupportedExtensions = null);
