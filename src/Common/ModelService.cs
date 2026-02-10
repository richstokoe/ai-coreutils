using System.ClientModel;
using OpenAI;

namespace AiCoreUtils.Common;

public static class ModelService
{
    public static async Task<List<string>> ListModelsAsync()
    {
        var endpoint = ConfigManager.GetEndpoint();

        var client = new OpenAIClient(
            new ApiKeyCredential("no-key-required"),
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

        var result = await client.GetOpenAIModelClient().GetModelsAsync();
        var models = result.Value
            .Select(m => m.Id)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return models;
    }
}
