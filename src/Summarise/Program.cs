using AiCoreUtils.Common;
using Microsoft.Agents.AI;

try
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: summarise <path-to-text-file>");
        return 1;
    }

    var filePath = Path.GetFullPath(args[0]);

    if (!File.Exists(filePath))
    {
        Console.Error.WriteLine($"File not found: {filePath}");
        return 1;
    }

    var content = await File.ReadAllTextAsync(filePath);

    if (string.IsNullOrWhiteSpace(content))
    {
        Console.Error.WriteLine("File is empty.");
        return 1;
    }

    var agent = await AgentFactory.CreateAgentAsync(
        instructions: """
            You are a summarisation tool. You receive the contents of a document and produce
            a clear, concise summary. Output only the summary, nothing else. No preamble,
            no sign-off. Match the length of the summary to the complexity of the input -
            short documents get a sentence or two, long documents get more.
            """,
        name: "Summariser");

    await foreach (var update in agent.RunStreamingAsync($"Summarise this document:\n\n{content}"))
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
