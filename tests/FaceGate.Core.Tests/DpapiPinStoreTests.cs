using FaceGate.Infrastructure.Storage;

namespace FaceGate.Core.Tests;

public sealed class DpapiPinStoreTests
{
    [Test]
    public void MissingFileReturnsNull()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"facegate_test_{Guid.NewGuid():N}.dat");
        try
        {
            var store = new DpapiPinStore(tempFile);
            Check.Eq(null, store.Load());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public void RoundTrip()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"facegate_test_{Guid.NewGuid():N}.dat");
        try
        {
            var store = new DpapiPinStore(tempFile);
            var verifier = "pbkdf2-sha256$600000$somesalt$somehashvalue";
            store.Save(verifier);

            var loaded = store.Load();
            Check.Eq(verifier, loaded);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public void EncryptedOnDisk()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"facegate_test_{Guid.NewGuid():N}.dat");
        try
        {
            var store = new DpapiPinStore(tempFile);
            var verifier = "pbkdf2-sha256$600000$secretdata$testpayload";
            store.Save(verifier);

            var rawBytes = File.ReadAllBytes(tempFile);
            var rawText = System.Text.Encoding.UTF8.GetString(rawBytes);

            // Plaintext verifier must not appear in the saved file
            Check.True(!rawText.Contains("testpayload"), "Saved file should be encrypted, not plaintext");
            Check.True(!rawText.Contains("pbkdf2-sha256"), "Header should be encrypted");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public void AtomicSaveLeavesNoTempFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"facegate_test_{Guid.NewGuid():N}.dat");
        try
        {
            var store = new DpapiPinStore(tempFile);
            store.Save("test_verifier_value");

            Check.True(File.Exists(tempFile), "Target file should exist");
            Check.True(!File.Exists(tempFile + ".tmp"), "Temp file should be cleaned up atomically");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Test]
    public void ClearDeletesFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"facegate_test_{Guid.NewGuid():N}.dat");
        try
        {
            var store = new DpapiPinStore(tempFile);
            store.Save("test_verifier_value");
            Check.True(File.Exists(tempFile), "File exists after save");

            store.Clear();
            Check.True(!File.Exists(tempFile), "File deleted after clear");
            Check.Eq(null, store.Load());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
