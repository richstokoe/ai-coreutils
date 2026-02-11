using System.ClientModel;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AiCoreUtils.Common;

public static class AgentFactory
{
    private static readonly HttpClient Http = new();

    public static async Task<AIAgent> CreateAgentAsync(string instructions, string name,
        IEnumerable<AITool>? tools = null)
    {
        var endpoint = ConfigManager.GetEndpoint();
        var model = ConfigManager.GetModel();

        await EnsureModelLoadedAsync(endpoint, model);

        var chatClient = new OpenAIClient(
                new ApiKeyCredential("no-key-required"),
                new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
            .GetChatClient(model)
            .AsIChatClient();

        return tools is not null
            ? chatClient.AsAIAgent(instructions: instructions, name: name, tools: [..tools])
            : chatClient.AsAIAgent(instructions: instructions, name: name);
    }

    private static async Task EnsureModelLoadedAsync(string endpoint, string model)
    {
        var baseUri = new Uri(endpoint);

        if (await IsModelLoadedAsync(baseUri, model))
            return;

        var loadUrl = new Uri(baseUri, "/api/v1/models/load");

        var payload = new
        {
            model,
            context_length = 16384,
            flash_attention = true,
            echo_load_config = true
        };

        var response = await Http.PostAsJsonAsync(loadUrl, payload);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<bool> IsModelLoadedAsync(Uri baseUri, string model)
    {
        var modelsUrl = new Uri(baseUri, "/api/v1/models");
        var response = await Http.GetAsync(modelsUrl);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        foreach (var entry in doc.RootElement.GetProperty("models").EnumerateArray())
        {
            if (entry.GetProperty("key").GetString() != model)
                continue;

            var instances = entry.GetProperty("loaded_instances");
            return instances.GetArrayLength() > 0;
        }

        return false;
    }
}
