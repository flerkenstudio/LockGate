using System.IO;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace LockGate.App.Services;

public static class IntruderCaptureService
{
    public static string GetSnapshotsDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var snapshotDir = Path.Combine(appData, "LockGate", "Snapshots");
        Directory.CreateDirectory(snapshotDir);
        return snapshotDir;
    }

    public static async Task<string?> CaptureSnapshotAsync(string reason)
    {
        try
        {
            var snapshotDir = GetSnapshotsDirectory();

            var fileName = $"intruder_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.jpg";
            var filePath = Path.Combine(snapshotDir, fileName);

            var capture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                PhotoCaptureSource = PhotoCaptureSource.Photo
            };

            var preferredCam = LockGateService.Instance.Config.PreferredCameraId;
            if (!string.IsNullOrEmpty(preferredCam))
            {
                settings.VideoDeviceId = preferredCam;
            }

            await capture.InitializeAsync(settings);

            var file = await StorageFile.GetFileFromPathAsync(
                await Task.Run(() =>
                {
                    File.WriteAllBytes(filePath, Array.Empty<byte>());
                    return filePath;
                }));

            var imgEncoding = ImageEncodingProperties.CreateJpeg();
            await capture.CapturePhotoToStorageFileAsync(imgEncoding, file);

            capture.Dispose();
            return filePath;
        }
        catch
        {
            // Camera not present, in use, or permission denied: gracefully fail closed without breaking app
            return null;
        }
    }
}
