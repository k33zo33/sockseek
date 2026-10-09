using System.Text.Json;

namespace Sockseek.Packager;

public static class ReleaseArtifactValidator
{
    public const string LicenseFileName = "LICENSE";
    public const string ThirdPartyNoticesFileName = "THIRD-PARTY-NOTICES";
    public const string SecurityFileName = "SECURITY.md";
    public const string BetaLimitationsFileName = "docs/beta-limitations.md";
    public const string MetadataFileName = "release-metadata.json";
    public const string SbomFileName = "sbom.spdx.json";
    public const string WindowsInstallerFileName = "install.ps1";
    public const string WindowsUninstallerFileName = "uninstall.ps1";

    public static ReleaseArtifactValidationResult Validate(
        string stagingDirectory,
        string desktopExecutableName,
        string daemonExecutableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(desktopExecutableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(daemonExecutableName);

        var errors = new List<string>();
        var fullStagingDirectory = Path.GetFullPath(stagingDirectory);
        if (!Directory.Exists(fullStagingDirectory))
        {
            errors.Add($"Staging directory does not exist: {fullStagingDirectory}");
            return new ReleaseArtifactValidationResult(fullStagingDirectory, errors);
        }

        RequireFile(fullStagingDirectory, LicenseFileName, errors);
        RequireFile(fullStagingDirectory, ThirdPartyNoticesFileName, errors);
        RequireFile(fullStagingDirectory, SecurityFileName, errors);
        RequireFile(fullStagingDirectory, BetaLimitationsFileName, errors);
        RequireFile(fullStagingDirectory, desktopExecutableName, errors);
        RequireFile(fullStagingDirectory, daemonExecutableName, errors);
        RequireFile(fullStagingDirectory, WindowsInstallerFileName, errors);
        RequireFile(fullStagingDirectory, WindowsUninstallerFileName, errors);
        ValidateMetadata(fullStagingDirectory, errors);
        ValidateSbom(fullStagingDirectory, errors);

        return new ReleaseArtifactValidationResult(fullStagingDirectory, errors);
    }

    private static void RequireFile(string stagingDirectory, string relativePath, List<string> errors)
    {
        if (Path.IsPathRooted(relativePath))
        {
            errors.Add($"Release artifact path must be relative: {relativePath}");
            return;
        }

        var artifactPath = Path.GetFullPath(Path.Combine(stagingDirectory, relativePath));
        if (!IsUnderDirectory(stagingDirectory, artifactPath))
        {
            errors.Add($"Release artifact path must stay inside staging directory: {relativePath}");
            return;
        }

        if (!File.Exists(artifactPath))
            errors.Add($"Missing required release artifact: {relativePath}");
    }

    private static bool IsUnderDirectory(string directory, string path)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var directoryPrefix = Path.GetFullPath(directory);
        if (!directoryPrefix.EndsWith(Path.DirectorySeparatorChar))
            directoryPrefix += Path.DirectorySeparatorChar;

        return path.StartsWith(directoryPrefix, comparison);
    }

    private static void ValidateMetadata(string stagingDirectory, List<string> errors)
    {
        var metadataPath = Path.Combine(stagingDirectory, MetadataFileName);
        if (!File.Exists(metadataPath))
        {
            errors.Add($"Missing required release artifact: {MetadataFileName}");
            return;
        }

        ReleaseMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<ReleaseMetadata>(
                File.ReadAllText(metadataPath),
                ReleaseMetadataJsonContext.Default.ReleaseMetadata);
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid {MetadataFileName}: {ex.Message}");
            return;
        }

        if (metadata is null)
        {
            errors.Add($"Invalid {MetadataFileName}: empty metadata.");
            return;
        }

        RequireMetadataValue(metadata.Version, "version", errors);
        RequireMetadataValue(metadata.Commit, "commit", errors);
        RequireMetadataValue(metadata.SourceUrl, "sourceUrl", errors);
        RequireMetadataValue(metadata.License, "license", errors);

        if (!string.Equals(metadata.License, "AGPL-3.0", StringComparison.Ordinal))
            errors.Add("release-metadata.json license must be AGPL-3.0.");

        if (!Uri.TryCreate(metadata.SourceUrl, UriKind.Absolute, out var sourceUri)
            || sourceUri.Scheme is not ("https" or "http"))
        {
            errors.Add("release-metadata.json sourceUrl must be an absolute HTTP(S) URL.");
        }
    }

    private static void RequireMetadataValue(string? value, string propertyName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"release-metadata.json must include '{propertyName}'.");
    }

    private static void ValidateSbom(string stagingDirectory, List<string> errors)
    {
        var sbomPath = Path.Combine(stagingDirectory, SbomFileName);
        if (!File.Exists(sbomPath))
        {
            errors.Add($"Missing required release artifact: {SbomFileName}");
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(sbomPath));
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid {SbomFileName}: {ex.Message}");
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{SbomFileName} must be a JSON object.");
                return;
            }

            RequireSbomString(root, "spdxVersion", errors);
            RequireSbomString(root, "SPDXID", errors);
            RequireSbomString(root, "name", errors);

            if (!root.TryGetProperty("packages", out var packages)
                || packages.ValueKind != JsonValueKind.Array
                || packages.GetArrayLength() == 0)
            {
                errors.Add($"{SbomFileName} must include at least one package.");
            }
        }
    }

    private static void RequireSbomString(JsonElement root, string propertyName, List<string> errors)
    {
        if (!root.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            errors.Add($"{SbomFileName} must include '{propertyName}'.");
        }
    }
}

public sealed record ReleaseArtifactValidationResult(
    string StagingDirectory,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record ReleaseMetadata(
    string Version,
    string Commit,
    string SourceUrl,
    string License);
