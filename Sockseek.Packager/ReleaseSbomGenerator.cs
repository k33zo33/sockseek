using System.Text.Encodings.Web;
using System.Text.Json;

namespace Sockseek.Packager;

public static class ReleaseSbomGenerator
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = true,
    };

    public static ReleaseSbomGenerationResult Generate(ReleaseSbomGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var repoRoot = Path.GetFullPath(request.RepositoryRoot);
        var outputPath = Path.GetFullPath(request.OutputPath);
        var errors = new List<string>();

        if (!Directory.Exists(repoRoot))
        {
            errors.Add($"Repository root does not exist: {repoRoot}");
            return new ReleaseSbomGenerationResult(outputPath, 0, errors);
        }

        var packages = ReadPackages(repoRoot, errors)
            .OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (packages.Length == 0)
            errors.Add("No resolved NuGet packages were found in packages.lock.json files.");

        if (errors.Count > 0)
            return new ReleaseSbomGenerationResult(outputPath, packages.Length, errors);

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        using var stream = File.Create(outputPath);
        using var writer = new Utf8JsonWriter(stream, WriterOptions);
        WriteDocument(writer, request, packages);
        writer.Flush();

        return new ReleaseSbomGenerationResult(outputPath, packages.Length, []);
    }

    private static IReadOnlyCollection<SbomPackage> ReadPackages(string repoRoot, List<string> errors)
    {
        var packages = new Dictionary<string, SbomPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var lockFile in Directory.EnumerateFiles(repoRoot, "packages.lock.json", SearchOption.AllDirectories)
                     .Where(path => !IsUnderBuildDirectory(repoRoot, path))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(lockFile));
                if (!document.RootElement.TryGetProperty("dependencies", out var frameworks)
                    || frameworks.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var framework in frameworks.EnumerateObject())
                {
                    if (framework.Value.ValueKind != JsonValueKind.Object)
                        continue;

                    foreach (var dependency in framework.Value.EnumerateObject())
                    {
                        if (dependency.Value.ValueKind != JsonValueKind.Object
                            || !dependency.Value.TryGetProperty("resolved", out var resolved)
                            || resolved.ValueKind != JsonValueKind.String
                            || string.IsNullOrWhiteSpace(resolved.GetString()))
                        {
                            continue;
                        }

                        var package = new SbomPackage(dependency.Name, resolved.GetString()!);
                        packages.TryAdd(package.Key, package);
                    }
                }
            }
            catch (JsonException ex)
            {
                errors.Add($"Invalid packages.lock.json '{Path.GetRelativePath(repoRoot, lockFile)}': {ex.Message}");
            }
        }

        return packages.Values;
    }

    private static bool IsUnderBuildDirectory(string repoRoot, string path)
    {
        var relativePath = Path.GetRelativePath(repoRoot, path);
        return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteDocument(Utf8JsonWriter writer, ReleaseSbomGenerationRequest request, IReadOnlyList<SbomPackage> packages)
    {
        writer.WriteStartObject();
        writer.WriteString("spdxVersion", "SPDX-2.3");
        writer.WriteString("dataLicense", "CC0-1.0");
        writer.WriteString("SPDXID", "SPDXRef-DOCUMENT");
        writer.WriteString("name", request.Name);
        writer.WriteString("documentNamespace", BuildDocumentNamespace(request));
        writer.WriteStartObject("creationInfo");
        writer.WriteStartArray("creators");
        writer.WriteStringValue("Tool: Sockseek.Packager");
        writer.WriteEndArray();
        writer.WriteString("created", DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        writer.WriteEndObject();
        writer.WriteStartArray("packages");
        foreach (var package in packages)
        {
            writer.WriteStartObject();
            writer.WriteString("name", package.Name);
            writer.WriteString("SPDXID", "SPDXRef-Package-" + SanitizeSpdxId(package.Name + "-" + package.Version));
            writer.WriteString("versionInfo", package.Version);
            writer.WriteString("downloadLocation", "NOASSERTION");
            writer.WriteBoolean("filesAnalyzed", false);
            writer.WriteString("licenseConcluded", "NOASSERTION");
            writer.WriteString("licenseDeclared", "NOASSERTION");
            writer.WriteString("copyrightText", "NOASSERTION");
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static string BuildDocumentNamespace(ReleaseSbomGenerationRequest request)
    {
        var source = request.SourceUrl.TrimEnd('/');
        return $"{source}/sbom/{Uri.EscapeDataString(request.Version)}/{Uri.EscapeDataString(request.Commit)}";
    }

    private static string SanitizeSpdxId(string value)
    {
        var chars = value.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-'
                ? character
                : '-');
        return string.Concat(chars).Trim('-');
    }

    private sealed record SbomPackage(string Name, string Version)
    {
        public string Key => Name + "@" + Version;
    }
}

public sealed record ReleaseSbomGenerationRequest(
    string RepositoryRoot,
    string OutputPath,
    string Name,
    string Version,
    string Commit,
    string SourceUrl);

public sealed record ReleaseSbomGenerationResult(
    string OutputPath,
    int PackageCount,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
