using System.ComponentModel;
using System.Text.Json;

namespace AiCoreUtils.Common.Tools;
public static class NetworkTools
{
    private static readonly HttpClient _httpClient = new();

    [Description("Get the user's public IP address. Returns the external/public IP address of the current machine.")]
    public static async Task<string> GetPublicIpAddress()
    {
        try
        {
            // Using ipify - a simple, free IP address API
            var response = await _httpClient.GetStringAsync("https://api.ipify.org?format=json");
            var json = JsonDocument.Parse(response);
            return json.RootElement.GetProperty("ip").GetString() ?? "Unable to determine IP address";
        }
        catch (Exception ex)
        {
            return $"Error fetching IP address: {ex.Message}";
        }
    }
}
