namespace Sockseek.Domain.Workflows;

public enum ResolutionMethod
{
    None = 0,
    ExactLocalMatch = 1,
    PreviousSourceMapping = 2,
    ManualReview = 3,
    SoulseekSearch = 4,
    DownloadedCandidate = 5,
}
