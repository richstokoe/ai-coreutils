using System.Text.Json;
using System.Text.RegularExpressions;
using AiCoreUtils.Common;
using AiCoreUtils.Common.Tools;
using Microsoft.Extensions.AI;

try
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: aicp <instruction>");
        Console.Error.WriteLine("Example: aicp \"all PDFs in this folder to ~/Documents\"");
        return 1;
    }

    var cwd = Directory.GetCurrentDirectory();
    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var instruction = string.Join(' ', args);

    // --- Step 1: Extract filesystem paths from args ---

    var pathTokens = new List<(string Original, string Resolved, PathKind Kind)>();
    var nonPathTokens = new List<string>();

    foreach (var arg in args)
    {
        if (arg.StartsWith('/') || arg.StartsWith("~/") || arg.StartsWith("./") || arg.StartsWith("../"))
        {
            var expanded = arg.StartsWith("~/")
                ? Path.Combine(homeDir, arg[2..])
                : arg;
            var resolved = Path.GetFullPath(expanded, cwd);

            if (File.Exists(resolved))
                pathTokens.Add((arg, resolved, PathKind.File));
            else if (Directory.Exists(resolved))
                pathTokens.Add((arg, resolved, PathKind.Directory));
            else
                nonPathTokens.Add(arg);
        }
        else
        {
            nonPathTokens.Add(arg);
        }
    }

    // Secondary scan: find paths embedded in natural language (e.g. "all PDFs in ~/Documents")
    var pathPattern = new Regex(@"(?:~/|/)[^\s""']+");
    foreach (Match match in pathPattern.Matches(instruction))
    {
        var token = match.Value;
        var expanded = token.StartsWith("~/")
            ? Path.Combine(homeDir, token[2..])
            : token;
        var resolved = Path.GetFullPath(expanded, cwd);

        if (pathTokens.Any(p => p.Resolved == resolved))
            continue;

        if (Directory.Exists(resolved))
            pathTokens.Add((token, resolved, PathKind.Directory));
        else if (File.Exists(resolved))
            pathTokens.Add((token, resolved, PathKind.File));
    }

    // --- Step 2: Choose enumeration mode ---

    var explicitFiles = pathTokens.Where(p => p.Kind == PathKind.File).ToList();
    var explicitDirs = pathTokens.Where(p => p.Kind == PathKind.Directory).ToList();

    var files = new List<string>();
    bool truncated = false;
    const int maxFiles = 200;
    const int maxDepth = 2;

    if (explicitFiles.Count > 0 && nonPathTokens.Count == 0)
    {
        // Mode A: Pure explicit paths (e.g. "aicp ~/Documents/file.txt /tmp/")
        // No enumeration needed -- source files are explicitly named.
        foreach (var f in explicitFiles)
            files.Add(f.Resolved);
    }
    else if (explicitDirs.Count > 0)
    {
        // Mode B: Directory reference (e.g. "aicp 'all PDFs in ~/Documents to /tmp'")
        // Enumerate the referenced directory, not cwd.
        foreach (var d in explicitDirs)
        {
            EnumerateFiles(d.Resolved, d.Resolved, maxDepth, maxFiles - files.Count, files);
        }
        truncated = files.Count >= maxFiles;
    }
    else
    {
        // Mode C: Pure natural language (e.g. "aicp 'move the ls manpage to temp'")
        // Enumerate cwd with conservative limits.
        EnumerateFiles(cwd, cwd, maxDepth, maxFiles, files);
        truncated = files.Count >= maxFiles;
    }

    if (files.Count == 0)
    {
        Console.Error.WriteLine("No files found matching the instruction.");
        return 1;
    }

    // --- Step 3: Call the LLM ---

    var tools = new[] { AIFunctionFactory.Create(DirectoryListTool.ListDirectory) };

    var agent = await AgentFactory.CreateAgentAsync(
        instructions: """
            You are a file copy planner. You receive a natural language description of some files
            or a directory to copy to another location. For example you may be asked to
            'copy all the PDFs in this directory to the home directory', which is the
            equivalent of running 'cp *.pdf ~/' on a Unix/MacOS/Linux machine.

            You have a ListDirectory tool available. Use it to inspect directories when you
            need to find files by date, size, or other attributes not in the basic file listing.

            You will value data integrity and safety above all else. If the request is too
            ambiguous, you must reject the request with an ACTIONABLE message for how
            the user can make a less ambiguous request.

            You must also protect the integrity of the system. Refuse to copy files that
            may result in the system not working. For example, don't copy a text file over
            the boot image in /boot.

            You must respond with ONLY a JSON array of copy operations. No markdown fencing,
            no explanation, no other text. Just the raw JSON array.

            Each element must be an object with exactly two string properties:
            - "source": the path of the file to copy, exactly as shown in the file listing
            - "destination": the full absolute path where the file should be copied to

            Rules:
            - Only copy file(s) and directory(ies) that were requested.
            - Expand ~ to the user's home directory in destination paths.
            - If the instruction says "to a folder", the destination should preserve the original
              filename inside that folder.
            - If no files match the instruction, return an empty array: []
            - Never invent files that are not in the listing.
            """,
        name: "FileCopyPlanner",
        tools: tools);

    var fileListing = string.Join('\n', files);
    var prompt = $"""
        Current directory: {cwd}
        Home directory: {homeDir}
        {(truncated ? $"File listing (truncated to first {files.Count} entries):" : "File listing:")}
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

    // --- Step 4: Parse the copy plan ---

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

    // --- Step 5: Validate and display plan ---

    var resolvedOps = new List<(string Source, string Destination)>();
    foreach (var op in operations)
    {
        var fullSource = Path.IsPathRooted(op.Source)
            ? op.Source
            : Path.GetFullPath(op.Source, cwd);

        if (!File.Exists(fullSource))
        {
            Console.Error.WriteLine($"Warning: source file does not exist, skipping: {op.Source}");
            continue;
        }

        // If the LLM returned a directory as the destination, append the filename
        var dest = Directory.Exists(op.Destination)
            ? Path.Combine(op.Destination, Path.GetFileName(fullSource))
            : op.Destination;

        resolvedOps.Add((fullSource, dest));
    }

    if (resolvedOps.Count == 0)
    {
        Console.WriteLine("No valid files to copy after validation.");
        return 0;
    }

    Console.WriteLine($"Planned copies ({resolvedOps.Count} file(s)):");
    Console.WriteLine();
    foreach (var (src, dst) in resolvedOps)
    {
        Console.WriteLine($"  {src} -> {dst}");
    }
    Console.WriteLine();

    // --- Step 6: Confirm ---

    Console.Write("Proceed? [y/N] ");
    var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (answer is not "y" and not "yes")
    {
        Console.WriteLine("Cancelled.");
        return 0;
    }

    // --- Step 7: Execute copies ---

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
            Console.WriteLine($"  Copied: {src} -> {dst}");
            copied++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Failed: {src} -> {dst}: {ex.Message}");
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
            results.Add(file);
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

enum PathKind { File, Directory }
