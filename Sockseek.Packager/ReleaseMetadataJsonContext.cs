using System.Text.Json.Serialization;

namespace Sockseek.Packager;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ReleaseMetadata))]
public sealed partial class ReleaseMetadataJsonContext : JsonSerializerContext;
