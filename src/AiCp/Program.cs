using System.Text.Json;
using AiCoreUtils.Common;
using Microsoft.Agents.AI;

try
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: aicp <instruction>");
        Console.Error.WriteLine("Example: aicp \"all PDFs in this folder to ~/Documents\"");
        return 1;
    }

    var instruction = string.Join(' ', args);
    var cwd = Directory.GetCurrentDirectory();
    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // Enumerate files in the current directory tree
    const int maxDepth = 5;
    const int maxFiles = 2000;

    var files = new List<string>();
    EnumerateFiles(cwd, cwd, maxDepth, maxFiles, files);

    if (files.Count == 0)
    {
        Console.Error.WriteLine("No files found in the current directory.");
        return 1;
    }

    var fileListing = string.Join('\n', files);
    var truncated = files.Count >= maxFiles;

    // Ask the LLM to plan the copy operations
    var agent = await AgentFactory.CreateAgentAsync(
        instructions: """
            You are a file copy planner. You receive a natural language description of some files
            or a directory to copy to another location. For example you may be asked to
            'copy all the PDFs in this directory to the home directory', which is the
            equivalent of running 'cp *.pdf ~/' on a Unix/MacOS/Linux machine.

            You will value data integrity and safety above all else. If the request is too
            ambiguous, you must reject the request with an ACTIONABLE message for how
            the user can make a less ambiguous request. 

            You must also protect the integrity of the system. Refuse to copy files that
            may result in the system not working. For example, don't copy a text file over
            the boot image in /boot. 

            You must respond with ONLY a JSON array of copy operations. No markdown fencing,
            no explanation, no other text. Just the raw JSON array.

            Each element must be an object with exactly two string properties:
            - "source": the relative path of the file to copy (must exist in the file listing)
            - "destination": the full absolute path where the file should be copied to

            Rules:
            - Only copy file(s) and directory(ies) that were requested.
            - Expand ~ to the user's home directory in destination paths.
            - If the instruction says "to a folder", the destination should preserve the original
              filename inside that folder.
            - If no files match the instruction, return an empty array: []
            - Never invent files that are not in the listing.
            """,
        name: "FileCopyPlanner");

    var prompt = $"""
        Current directory: {cwd}
        Home directory: {homeDir}
        {(truncated ? $"File listing (truncated to first {maxFiles} files):" : "File listing:")}
        {fileListing}

        Instruction: {instruction}
        """;

    var response = await agent.RunAsync(prompt);
    var responseText = response.ToString().Trim();

    // Strip markdown code fences if the model added them
    if (responseText.StartsWith("```"))
    {
        var lines = responseText.Split('\n');
        responseText = string.Join('\n',
            lines.Where(l => !l.TrimStart().StartsWith("```")));
    }

    // Parse the copy plan
    List<CopyOperation>? operations;
    try
    {
        operations = JsonSerializer.Deserialize<List<CopyOperation>>(responseText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (JsonException)
    {
        Console.Error.WriteLine("Could not parse copy plan from model response:");
        Console.Error.WriteLine(responseText);
        return 1;
    }

    if (operations is null || operations.Count == 0)
    {
        Console.WriteLine("No files matched the instruction.");
        return 0;
    }

    // Validate sources and build the resolved plan
    var resolvedOps = new List<(string Source, string Destination)>();
    foreach (var op in operations)
    {
        var fullSource = Path.GetFullPath(op.Source, cwd);
        if (!File.Exists(fullSource))
        {
            Console.Error.WriteLine($"Warning: source file does not exist, skipping: {op.Source}");
            continue;
        }
        resolvedOps.Add((fullSource, op.Destination));
    }

    if (resolvedOps.Count == 0)
    {
        Console.WriteLine("No valid files to copy after validation.");
        return 0;
    }

    // Display the plan
    Console.WriteLine($"Planned copies ({resolvedOps.Count} file(s)):");
    Console.WriteLine();
    foreach (var (src, dst) in resolvedOps)
    {
        Console.WriteLine($"  {Path.GetRelativePath(cwd, src)} -> {dst}");
    }
    Console.WriteLine();

    // Confirm
    Console.Write("Proceed? [y/N] ");
    var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (answer is not "y" and not "yes")
    {
        Console.WriteLine("Cancelled.");
        return 0;
    }

    // Execute copies
    var copied = 0;
    var failed = 0;

    foreach (var (src, dst) in resolvedOps)
    {
        try
        {
            var destDir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            File.Copy(src, dst, overwrite: false);
            Console.WriteLine($"  Copied: {Path.GetRelativePath(cwd, src)} -> {dst}");
            copied++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Failed: {Path.GetRelativePath(cwd, src)} -> {dst}: {ex.Message}");
            failed++;
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Done. {copied} copied, {failed} failed.");
    return failed > 0 ? 1 : 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static void EnumerateFiles(string root, string current, int remainingDepth, int maxFiles, List<string> results)
{
    if (remainingDepth < 0 || results.Count >= maxFiles)
        return;

    try
    {
        foreach (var file in Directory.EnumerateFiles(current))
        {
            if (results.Count >= maxFiles) return;
            results.Add(Path.GetRelativePath(root, file));
        }

        foreach (var dir in Directory.EnumerateDirectories(current))
        {
            if (results.Count >= maxFiles) return;
            EnumerateFiles(root, dir, remainingDepth - 1, maxFiles, results);
        }
    }
    catch (UnauthorizedAccessException)
    {
        // Skip directories we cannot read
    }
}

record CopyOperation(string Source, string Destination);
