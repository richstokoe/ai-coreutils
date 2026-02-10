using AiCoreUtils.Common;
using Microsoft.Agents.AI;

try
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: llm <prompt>");
        return 1;
    }

    var prompt = string.Join(' ', args);

    var agent = await AgentFactory.CreateAgentAsync(
        instructions: "You are a helpful assistant. Be concise and generally dispassionate. Don't be sycophantic.",
        name: "Chat");

    await foreach (var update in agent.RunStreamingAsync(prompt))
    {
        Console.Write(update);
    }

    Console.WriteLine();
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
