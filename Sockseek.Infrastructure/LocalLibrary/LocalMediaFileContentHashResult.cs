namespace Sockseek.Infrastructure.LocalLibrary;

public sealed record LocalMediaFileContentHashResult(
    int ConsideredFiles,
    int HashedFiles,
    int MissingFiles,
    int FailedFiles);
