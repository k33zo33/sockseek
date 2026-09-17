namespace Sockseek.Infrastructure.Persistence;

public sealed record LocalLibrarySearchRequest(
    string? SearchText = null,
    int Offset = 0,
    int Limit = 100,
    bool IncludeMissing = true);
