using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Sockseek.Api;
using Sockseek.Application.Soulseek;

namespace Sockseek.Server;

public sealed class ServerSoulseekEngineGateway : ISoulseekEngineGateway
{
    private readonly EngineSupervisor supervisor;
    private readonly ServerEventBroadcaster? broadcaster;

    public ServerSoulseekEngineGateway(EngineSupervisor supervisor, ServerEventBroadcaster? broadcaster = null)
    {
        this.supervisor = supervisor;
        this.broadcaster = broadcaster;
    }

    public async Task<SearchHandle> StartTrackSearchAsync(TrackSearchRequest request, CancellationToken cancellationToken)
    {
        var summary = await supervisor.SubmitTrackSearchJobAsync(
            new SubmitTrackSearchJobRequestDto(
                new SongQueryDto(request.Artist, request.Title, request.Album)),
            cancellationToken);

        return new SearchHandle(summary.WorkflowId, summary.JobId);
    }

    public async Task<SearchHandle> StartAlbumSearchAsync(AlbumSearchRequest request, CancellationToken cancellationToken)
    {
        var summary = await supervisor.SubmitAlbumSearchJobAsync(
            new SubmitAlbumSearchJobRequestDto(
                new AlbumQueryDto(request.Artist, request.Album, request.FilterText)),
            cancellationToken);

        return new SearchHandle(summary.WorkflowId, summary.JobId);
    }

    public async Task<DownloadHandle> StartDownloadAsync(CandidateReference candidate, DownloadOptions options, CancellationToken cancellationToken)
    {
        var summaries = await supervisor.StartFileDownloadsAsync(
            candidate.SourceJobId,
            new StartFileDownloadsRequestDto(
                [new FileCandidateRefDto(candidate.Username, candidate.Filename)],
                CreateSubmissionOptions(options)),
            cancellationToken);

        var summary = summaries?.SingleOrDefault()
            ?? throw new InvalidOperationException("The requested candidate could not be started as a download.");

        return new DownloadHandle(summary.WorkflowId, summary.JobId);
    }

    public Task CancelJobAsync(Guid engineJobId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!supervisor.CancelJob(engineJobId))
            throw new InvalidOperationException($"Job '{engineJobId}' could not be cancelled.");

        return Task.CompletedTask;
    }

    public Task<bool> TryNextCandidateAsync(Guid engineJobId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(supervisor.TryNextCandidate(engineJobId));
    }

    public Task<JobSnapshot?> GetJobAsync(Guid engineJobId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(supervisor.StateStore.GetJobSummary(engineJobId) is { } summary
            ? ToJobSnapshot(summary)
            : null);
    }

    public async IAsyncEnumerable<EngineEventEnvelope> SubscribeAsync(
        Guid workflowId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (broadcaster == null)
            yield break;

        var channel = Channel.CreateUnbounded<EngineEventEnvelope>();
        void Handler(ServerEventEnvelopeDto envelope)
        {
            if (envelope.WorkflowId != workflowId)
                return;

            if (TryMapEnvelope(envelope, out var mapped))
                channel.Writer.TryWrite(mapped);
        }

        broadcaster.EventPublished += Handler;

        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken))
            {
                while (channel.Reader.TryRead(out var envelope))
                    yield return envelope;
            }
        }
        finally
        {
            broadcaster.EventPublished -= Handler;
            channel.Writer.TryComplete();
        }
    }

    private static bool TryMapEnvelope(ServerEventEnvelopeDto envelope, out EngineEventEnvelope mapped)
    {
        JobSnapshot? snapshot = envelope.Payload switch
        {
            JobSummaryDto summary => ToJobSnapshot(summary),
            JobDetailDto detail => ToJobSnapshot(detail.Summary),
            JobStartedEventDto started => ToJobSnapshot(started.Summary),
            JobStatusEventDto status => ToJobSnapshot(status.Summary),
            JobMessageEventDto message => ToJobSnapshot(message.Summary),
            JobActivityChangedEventDto activity => ToJobSnapshot(activity.Summary),
            AlbumDownloadStartedEventDto albumStarted => ToJobSnapshot(albumStarted.Summary),
            AlbumTrackDownloadStartedEventDto albumTrackStarted => ToJobSnapshot(albumTrackStarted.Summary),
            AlbumStateChangedEventDto albumState => ToJobSnapshot(albumState.Summary),
            JobFolderRetrievingEventDto retrieving => ToJobSnapshot(retrieving.Summary),
            TrackBatchResolvedEventDto batchResolved => ToJobSnapshot(batchResolved.Summary),
            _ => null,
        };

        if (snapshot == null)
        {
            mapped = null!;
            return false;
        }

        mapped = new EngineEventEnvelope(
            Guid.NewGuid(),
            envelope.Type,
            envelope.OccurredAtUtc,
            $"server-event-{envelope.Sequence}",
            envelope.WorkflowId,
            snapshot.EngineJobId,
            envelope.Sequence,
            snapshot);
        return true;
    }

    private static SubmissionOptionsDto? CreateSubmissionOptions(DownloadOptions options)
    {
        var profileNames = string.IsNullOrWhiteSpace(options.ProfileName)
            ? null
            : new[] { options.ProfileName };

        return options.OutputParentDir == null && profileNames == null
            ? null
            : new SubmissionOptionsDto(
                OutputParentDir: options.OutputParentDir,
                ProfileNames: profileNames);
    }

    private static JobSnapshot ToJobSnapshot(JobSummaryDto summary)
        => new(
            summary.JobId,
            summary.WorkflowId,
            ToJobKind(summary.Kind),
            ToJobState(summary.LifecycleState, summary.TerminalOutcome),
            summary.ItemName ?? summary.QueryText ?? summary.Kind.ToString());

    private static SoulseekJobKind ToJobKind(ServerJobKind kind)
        => kind switch
        {
            ServerJobKind.AlbumAggregate => SoulseekJobKind.AlbumSearch,
            ServerJobKind.Search => SoulseekJobKind.TrackSearch,
            ServerJobKind.Aggregate => SoulseekJobKind.TrackSearch,
            ServerJobKind.Song => SoulseekJobKind.Download,
            ServerJobKind.Album => SoulseekJobKind.Download,
            ServerJobKind.RetrieveFolder => SoulseekJobKind.Download,
            _ => SoulseekJobKind.TrackSearch,
        };

    private static SoulseekJobState ToJobState(ServerJobLifecycleState lifecycleState, ServerJobTerminalOutcome terminalOutcome)
        => lifecycleState switch
        {
            ServerJobLifecycleState.Pending => SoulseekJobState.Queued,
            ServerJobLifecycleState.Running => SoulseekJobState.Running,
            ServerJobLifecycleState.AwaitingSelection => SoulseekJobState.Running,
            ServerJobLifecycleState.Terminal => terminalOutcome switch
            {
                ServerJobTerminalOutcome.Succeeded => SoulseekJobState.Succeeded,
                ServerJobTerminalOutcome.Cancelled => SoulseekJobState.Cancelled,
                _ => SoulseekJobState.Failed,
            },
            _ => SoulseekJobState.Running,
        };
}
