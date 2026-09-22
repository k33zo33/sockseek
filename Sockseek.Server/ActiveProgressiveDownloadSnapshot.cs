using Sockseek.Player;

namespace Sockseek.Server;

public sealed record ActiveProgressiveDownloadSnapshot(
    Guid JobId,
    Guid WorkflowId,
    ProgressiveMediaSource Source,
    ProgressiveBufferSnapshot Buffer);
