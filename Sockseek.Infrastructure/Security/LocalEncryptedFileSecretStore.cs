using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sockseek.Application.Security;

namespace Sockseek.Infrastructure.Security;

public sealed class LocalEncryptedFileSecretStore : ISecretStore
{
    private const string Scheme = "secret://local-encrypted/";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int EnvelopeVersion = 1;
    private const string KeyFileName = "master.key";
    private readonly string directory;
    private readonly byte[] key;

    public LocalEncryptedFileSecretStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        this.directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(this.directory);
        SetDirectoryPermissions(this.directory);
        key = LoadOrCreateKey(Path.Combine(this.directory, KeyFileName));
    }

    public async Task<string> SaveAsync(
        SecretStoreSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        SecretStoreValidation.Validate(request);

        string id = Guid.NewGuid().ToString("N");
        string reference = Scheme + id;
        var payload = new PersistedSecret(
            reference,
            request.ProviderId,
            new Dictionary<string, string>(request.Secrets, StringComparer.Ordinal),
            request.ExpiresAtUtc,
            DateTimeOffset.UtcNow);
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(payload);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(reference));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        byte[] envelope = JsonSerializer.SerializeToUtf8Bytes(
            new EncryptedEnvelope(EnvelopeVersion, nonce, tag, ciphertext));
        string path = GetPath(id);
        string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, envelope, cancellationToken);
            SetFilePermissions(temporaryPath);
            File.Move(temporaryPath, path);
            return reference;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async Task<SecretStoreEntry?> ReadAsync(
        string secretReference,
        CancellationToken cancellationToken = default)
    {
        string id = GetId(secretReference);
        string path = GetPath(id);
        if (!File.Exists(path))
            return null;

        byte[] serializedEnvelope = await File.ReadAllBytesAsync(path, cancellationToken);
        var envelope = JsonSerializer.Deserialize<EncryptedEnvelope>(serializedEnvelope)
            ?? throw new InvalidDataException("Encrypted secret payload is invalid.");
        if (envelope.Version != EnvelopeVersion
            || envelope.Nonce.Length != NonceSize
            || envelope.Tag.Length != TagSize)
        {
            throw new InvalidDataException("Encrypted secret payload has an unsupported format.");
        }

        byte[] plaintext = new byte[envelope.Ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                envelope.Nonce,
                envelope.Ciphertext,
                envelope.Tag,
                plaintext,
                Encoding.UTF8.GetBytes(secretReference));

            var payload = JsonSerializer.Deserialize<PersistedSecret>(plaintext)
                ?? throw new InvalidDataException("Decrypted secret payload is invalid.");
            if (!StringComparer.Ordinal.Equals(payload.SecretReference, secretReference))
                throw new InvalidDataException("Secret reference does not match the encrypted payload.");

            return new SecretStoreEntry(
                payload.SecretReference,
                payload.Secrets,
                payload.ExpiresAtUtc);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidDataException("Encrypted secret payload authentication failed.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public Task<bool> DeleteAsync(
        string secretReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetPath(GetId(secretReference));
        if (!File.Exists(path))
            return Task.FromResult(false);

        File.Delete(path);
        return Task.FromResult(true);
    }

    private string GetPath(string id)
        => Path.Combine(directory, id + ".secret");

    private static string GetId(string secretReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretReference);
        if (!secretReference.StartsWith(Scheme, StringComparison.Ordinal))
            throw new ArgumentException("Secret reference is not a local encrypted reference.", nameof(secretReference));

        string id = secretReference[Scheme.Length..];
        if (!Guid.TryParseExact(id, "N", out _))
            throw new ArgumentException("Secret reference is malformed.", nameof(secretReference));
        return id;
    }

    private static byte[] LoadOrCreateKey(string path)
    {
        if (File.Exists(path))
            return ReadAndValidateKey(path);

        byte[] candidate = RandomNumberGenerator.GetBytes(KeySize);
        try
        {
            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(candidate);
                stream.Flush(flushToDisk: true);
                SetFilePermissions(path);
                return candidate;
            }
            catch (IOException) when (File.Exists(path))
            {
                CryptographicOperations.ZeroMemory(candidate);
                return ReadAndValidateKey(path);
            }
        }
        catch
        {
            CryptographicOperations.ZeroMemory(candidate);
            throw;
        }
    }

    private static byte[] ReadAndValidateKey(string path)
    {
        SetFilePermissions(path);
        byte[] storedKey = File.ReadAllBytes(path);
        if (storedKey.Length != KeySize)
        {
            CryptographicOperations.ZeroMemory(storedKey);
            throw new InvalidDataException("Local secret-store key has an invalid length.");
        }

        return storedKey;
    }

    private static void SetDirectoryPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void SetFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private sealed record EncryptedEnvelope(
        int Version,
        byte[] Nonce,
        byte[] Tag,
        byte[] Ciphertext);

    private sealed record PersistedSecret(
        string SecretReference,
        string ProviderId,
        IReadOnlyDictionary<string, string> Secrets,
        DateTimeOffset? ExpiresAtUtc,
        DateTimeOffset CreatedAtUtc);
}
