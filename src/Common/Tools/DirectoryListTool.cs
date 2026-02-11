using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AiCoreUtils.Common.Tools;

public class DirectoryListTool
{
    [Description("Lists files and directories at the given path, including size and modification time. Use this to inspect directory contents, find files by name, or determine which file is newest/oldest/largest.")]
    public static string ListDirectory(
        [Description("The absolute path of the directory to list")] string path,
        [Description("Additional flags to pass to the list command (e.g. '-lt' to sort by time, '-S' to sort by size, '-R' for recursive)")] string flags = "-l")
    {
        if (!Directory.Exists(path))
            return $"Error: directory does not exist: {path}";

        string fileName;
        string arguments;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileName = "cmd.exe";
            arguments = $"/c dir {flags} \"{path}\"";
        }
        else
        {
            fileName = "ls";
            arguments = $"{flags} \"{path}\"";
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            return $"Error: {error.Trim()}";

        return output;
    }
}
