namespace Sockseek.Application.Soulseek;

public interface ISoulseekEngineGateway
{
    Task<SearchHandle> StartTrackSearchAsync(TrackSearchRequest request, CancellationToken cancellationToken);
    Task<SearchHandle> StartAlbumSearchAsync(AlbumSearchRequest request, CancellationToken cancellationToken);
    Task<DownloadHandle> StartDownloadAsync(CandidateReference candidate, DownloadOptions options, CancellationToken cancellationToken);
    Task CancelJobAsync(Guid engineJobId, CancellationToken cancellationToken);
    Task<bool> TryNextCandidateAsync(Guid engineJobId, CancellationToken cancellationToken);
    Task<JobSnapshot?> GetJobAsync(Guid engineJobId, CancellationToken cancellationToken);
    IAsyncEnumerable<EngineEventEnvelope> SubscribeAsync(Guid workflowId, CancellationToken cancellationToken);
}
