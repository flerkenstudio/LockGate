using System.Security.Cryptography;
using System.Text;
using FaceGate.Core.Security;

namespace FaceGate.Infrastructure.Storage;

/// <summary>
/// Persists the PIN verifier encrypted with DPAPI (CurrentUser scope) under %LOCALAPPDATA%\FaceGate\pin.dat.
/// Never stores plaintext PINs or unencrypted verifier hashes.
/// </summary>
public sealed class DpapiPinStore : IPinStore
{
    static readonly byte[] Entropy = "FaceGate.PIN.Entropy.v1"u8.ToArray();
    readonly string _filePath;

    public DpapiPinStore(string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "FaceGate");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "pin.dat");
        }
        else
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _filePath = filePath;
        }
    }

    public string? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            var encrypted = File.ReadAllBytes(_filePath);
            if (encrypted.Length == 0) return null;
            var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            // Decryption failed (e.g. file modified or from another user/machine). Fail closed.
            return null;
        }
    }

    public void Save(string verifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifier);

        var plain = Encoding.UTF8.GetBytes(verifier);
        var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

        var tempPath = _filePath + ".tmp";
        File.WriteAllBytes(tempPath, encrypted);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            try { File.Delete(_filePath); }
            catch { /* Ignore */ }
        }
    }
}
