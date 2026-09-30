using Windows.Security.Credentials.UI;

namespace LockGate.App.Services;

public static class WindowsHelloService
{
    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            var status = await UserConsentVerifier.CheckAvailabilityAsync();
            return status == UserConsentVerifierAvailability.Available;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<string> GetDetailedStatusAsync()
    {
        try
        {
            var status = await UserConsentVerifier.CheckAvailabilityAsync();
            return status switch
            {
                UserConsentVerifierAvailability.Available => "Available",
                UserConsentVerifierAvailability.DeviceNotPresent => "DeviceNotPresent",
                UserConsentVerifierAvailability.NotConfiguredForUser => "NotConfiguredForUser",
                UserConsentVerifierAvailability.DisabledByPolicy => "DisabledByPolicy",
                UserConsentVerifierAvailability.DeviceBusy => "DeviceBusy",
                _ => status.ToString()
            };
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public static async Task<bool> VerifyAsync(string message = "Unlock protected application")
    {
        try
        {
            var result = await UserConsentVerifier.RequestVerificationAsync(message);
            return result == UserConsentVerificationResult.Verified;
        }
        catch
        {
            return false;
        }
    }
}
