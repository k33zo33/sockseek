using System.Runtime.InteropServices;
using System.Text.Json;
using Sockseek.Application.Security;

namespace Sockseek.Infrastructure.Security;

public sealed class WindowsDpapiSecretStore : ISecretStore
{
    private const string Scheme = "secret://windows-dpapi/";
    private readonly string directory;

    public WindowsDpapiSecretStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows DPAPI secret store is only available on Windows.");

        this.directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(this.directory);
    }

    public static bool IsAvailable()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            _ = WindowsDataProtection.Unprotect(WindowsDataProtection.Protect([1]));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<string> SaveAsync(SecretStoreSaveRequest request, CancellationToken cancellationToken = default)
    {
        SecretStoreValidation.Validate(request);

        var id = Guid.NewGuid().ToString("N");
        var reference = Scheme + id;
        var payload = new PersistedSecret(
            reference,
            request.ProviderId,
            new Dictionary<string, string>(request.Secrets, StringComparer.Ordinal),
            request.ExpiresAtUtc,
            DateTimeOffset.UtcNow);
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        var encrypted = WindowsDataProtection.Protect(json);

        await File.WriteAllBytesAsync(GetPath(id), encrypted, cancellationToken);
        return reference;
    }

    public async Task<SecretStoreEntry?> ReadAsync(string secretReference, CancellationToken cancellationToken = default)
    {
        var id = GetId(secretReference);
        var path = GetPath(id);
        if (!File.Exists(path))
            return null;

        var encrypted = await File.ReadAllBytesAsync(path, cancellationToken);
        var json = WindowsDataProtection.Unprotect(encrypted);
        var payload = JsonSerializer.Deserialize<PersistedSecret>(json)
            ?? throw new InvalidDataException("Secret payload is invalid.");

        if (!StringComparer.Ordinal.Equals(payload.SecretReference, secretReference))
            throw new InvalidDataException("Secret reference does not match the encrypted payload.");

        return new SecretStoreEntry(
            payload.SecretReference,
            payload.Secrets,
            payload.ExpiresAtUtc);
    }

    public Task<bool> DeleteAsync(string secretReference, CancellationToken cancellationToken = default)
    {
        var id = GetId(secretReference);
        var path = GetPath(id);
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
            throw new ArgumentException("Secret reference is not a Windows DPAPI reference.", nameof(secretReference));

        var id = secretReference[Scheme.Length..];
        if (!Guid.TryParseExact(id, "N", out _))
            throw new ArgumentException("Secret reference is malformed.", nameof(secretReference));
        return id;
    }

    private sealed record PersistedSecret(
        string SecretReference,
        string ProviderId,
        IReadOnlyDictionary<string, string> Secrets,
        DateTimeOffset? ExpiresAtUtc,
        DateTimeOffset CreatedAtUtc);

    private static class WindowsDataProtection
    {
        private const int CryptProtectUiForbidden = 0x1;

        public static byte[] Protect(byte[] data)
            => ProtectOrUnprotect(data, protect: true);

        public static byte[] Unprotect(byte[] data)
            => ProtectOrUnprotect(data, protect: false);

        private static byte[] ProtectOrUnprotect(byte[] data, bool protect)
        {
            ArgumentNullException.ThrowIfNull(data);

            var input = DataBlob.From(data);
            try
            {
                var succeeded = protect
                    ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out var output)
                    : CryptUnprotectData(ref input, out _, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output);
                if (!succeeded)
                    throw new InvalidOperationException($"Windows DPAPI operation failed with error {Marshal.GetLastWin32Error()}.");

                try
                {
                    var result = new byte[output.Size];
                    Marshal.Copy(output.Data, result, 0, result.Length);
                    return result;
                }
                finally
                {
                    LocalFree(output.Data);
                }
            }
            finally
            {
                input.Free();
            }
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string? dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            out DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            out IntPtr dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Size;
            public IntPtr Data;

            public static DataBlob From(byte[] bytes)
            {
                var blob = new DataBlob
                {
                    Size = bytes.Length,
                    Data = Marshal.AllocHGlobal(bytes.Length),
                };
                Marshal.Copy(bytes, 0, blob.Data, bytes.Length);
                return blob;
            }

            public readonly void Free()
            {
                if (Data != IntPtr.Zero)
                    Marshal.FreeHGlobal(Data);
            }
        }
    }
}
