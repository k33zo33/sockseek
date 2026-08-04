using Sockseek.Domain.Accounts;
using Sockseek.Domain.Common;

namespace Sockseek.Domain.Tracks;

public sealed class TrackSource
{
    public TrackSource(ExternalProvider provider, string externalId, string? externalUrl, string? rawMetadataJson)
    {
        Id = EntityId.New();
        Provider = provider;
        ExternalId = Require(externalId, nameof(externalId));
        ExternalUrl = Normalize(externalUrl);
        RawMetadataJson = Normalize(rawMetadataJson);
    }

    public EntityId Id { get; }
    public ExternalProvider Provider { get; }
    public string ExternalId { get; }
    public string? ExternalUrl { get; private set; }
    public string? RawMetadataJson { get; private set; }

    public void UpdateMetadata(string? externalUrl, string? rawMetadataJson)
    {
        ExternalUrl = Normalize(externalUrl);
        RawMetadataJson = Normalize(rawMetadataJson);
    }

    private static string Require(string value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} is required.", paramName)
            : value.Trim();

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
