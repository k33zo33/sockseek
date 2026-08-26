namespace Sockseek.Infrastructure.Persistence.Entities;

using Sockseek.Infrastructure.Persistence.Abstractions;

public sealed class DownloadWorkflowEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid EngineJobId { get; set; }
    public Guid? PlaylistItemId { get; set; }
    public int Status { get; set; }
    public string? OutputPath { get; set; }
    public string? CandidateJson { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public PlaylistItemEntity? PlaylistItem { get; set; }
}
