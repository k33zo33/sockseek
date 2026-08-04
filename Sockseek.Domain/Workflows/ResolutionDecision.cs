namespace Sockseek.Domain.Workflows;

public enum ResolutionDecision
{
    None = 0,
    AutoMatched = 1,
    UserApproved = 2,
    UserRejected = 3,
    DownloadRequested = 4,
    Failed = 5,
}
