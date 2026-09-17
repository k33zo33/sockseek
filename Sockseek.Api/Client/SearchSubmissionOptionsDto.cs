namespace Sockseek.Api;

public sealed record SearchSubmissionOptionsDto(
    IReadOnlyList<string>? ProfileNames = null,
    int? MinBitrate = null,
    IReadOnlyList<string>? Formats = null);
