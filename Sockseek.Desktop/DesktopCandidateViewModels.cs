using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopFileCandidateViewModel
{
    public DesktopFileCandidateViewModel(FileCandidateDto candidate)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        MetadataSummary = BuildMetadataSummary(candidate);
    }

    public FileCandidateDto Candidate { get; }

    public FileCandidateRefDto Ref => Candidate.Ref;

    public string Username => Candidate.Username;

    public string Filename => Candidate.Filename;

    public PeerInfoDto Peer => Candidate.Peer;

    public string MetadataSummary { get; }

    private static string BuildMetadataSummary(FileCandidateDto candidate)
    {
        var parts = new List<string>
        {
            candidate.Peer.HasFreeUploadSlot switch
            {
                true => "slot free",
                false => "slot busy",
                _ => "slot unknown"
            }
        };

        if (candidate.Peer.UploadSpeed is { } speed)
            parts.Add($"speed {speed} B/s");

        var format = FormatExtension(candidate.Extension, candidate.Filename);
        if (format is not null)
            parts.Add($"format {format}");

        var bitRate = candidate.BitRate ?? GetAttributeValue(candidate, "BitRate");
        if (bitRate is { } bitRateValue)
            parts.Add($"{bitRateValue} kbps");

        var sampleRate = candidate.SampleRate ?? GetAttributeValue(candidate, "SampleRate");
        if (sampleRate is { } sampleRateValue)
            parts.Add($"{sampleRateValue} Hz");

        var bitDepth = GetAttributeValue(candidate, "BitDepth");
        if (bitDepth is { } bitDepthValue)
            parts.Add($"{bitDepthValue} bit");

        var length = candidate.Length ?? GetAttributeValue(candidate, "Length");
        if (length is { } lengthValue)
            parts.Add(FormatDuration(lengthValue));

        return string.Join(" | ", parts);
    }

    private static int? GetAttributeValue(FileCandidateDto candidate, string type)
        => candidate.Attributes?.FirstOrDefault(attribute =>
            string.Equals(attribute.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? FormatExtension(string? extension, string filename)
    {
        var value = string.IsNullOrWhiteSpace(extension)
            ? Path.GetExtension(filename)
            : extension;

        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().TrimStart('.').ToLowerInvariant();
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds < 0)
            return "duration unknown";

        var value = TimeSpan.FromSeconds(seconds);
        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"m\:ss");
    }
}

public sealed class DesktopAlbumFolderViewModel
{
    public DesktopAlbumFolderViewModel(AlbumFolderDto folder)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        MetadataSummary = BuildMetadataSummary(folder);
    }

    public AlbumFolderDto Folder { get; }

    public AlbumFolderRefDto Ref => Folder.Ref;

    public string Username => Folder.Username;

    public string FolderPath => Folder.FolderPath;

    public PeerInfoDto Peer => Folder.Peer;

    public int FileCount => Folder.FileCount;

    public int AudioFileCount => Folder.AudioFileCount;

    public string MetadataSummary { get; }

    private static string BuildMetadataSummary(AlbumFolderDto folder)
    {
        var parts = new List<string>
        {
            folder.Peer.HasFreeUploadSlot switch
            {
                true => "slot free",
                false => "slot busy",
                _ => "slot unknown"
            }
        };

        if (folder.Peer.UploadSpeed is { } speed)
            parts.Add($"speed {speed} B/s");

        parts.Add($"{folder.AudioFileCount} audio files");
        parts.Add($"{folder.FileCount} total files");

        return string.Join(" | ", parts);
    }
}
