using AiCoreUtils.Common;
using AiCoreUtils.Common.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

try
{
    if (args is ["--help"])
    {
        Console.Error.WriteLine("Usage: ails [natural language description]");
        Console.Error.WriteLine("       ails \"show all files sorted by size, human readable\"");
        Console.Error.WriteLine("       ails                  (lists current directory)");
        return 0;
    }

    var prompt = args.Length > 0 ? string.Join(' ', args) : "list the current directory";
    var cwd = Directory.GetCurrentDirectory();

    var tools = new[] { AIFunctionFactory.Create(DirectoryListTool.ListDirectory) };

    var agent = await AgentFactory.CreateAgentAsync(
        instructions: """
            You are a directory listing tool. The user describes what they want to see
            in natural language. Use the ListDirectory tool to execute the appropriate
            listing command with the correct flags. Default to the current working directory
            if the user does not specify a path. Output only the listing results with no
            additional commentary, preamble, or sign-off.
            """,
        name: "AiLs",
        tools: tools);

    await foreach (var update in agent.RunStreamingAsync(
        $"Current directory: {cwd}\n\nRequest: {prompt}"))
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
