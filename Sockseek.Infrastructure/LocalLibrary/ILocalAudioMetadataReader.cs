namespace Sockseek.Infrastructure.LocalLibrary;

public interface ILocalAudioMetadataReader
{
    Task<LocalAudioMetadata> ReadAsync(string path, CancellationToken cancellationToken = default);
}
