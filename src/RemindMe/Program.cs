using System.Diagnostics;
using System.Globalization;
using AiCoreUtils.Common;
using Microsoft.Agents.AI;

try
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: remindme <when> <message>");
        Console.Error.WriteLine("Example: remindme \"in 2 hours\" \"Take the pizza out\"");
        return 1;
    }

    var whenText = args[0];
    var message = args[1];

    var agent = await AgentFactory.CreateAgentAsync(
        instructions: $"""
            You are a date/time parser. The current date and time is {DateTime.Now:yyyy-MM-dd HH:mm:ss} ({DateTime.Now:dddd}).
            The user will give you a natural language time expression.
            You must respond with ONLY a single datetime in the exact format: yyyy-MM-dd HH:mm:ss
            No other text, no explanation, no quotes. Just the datetime.
            If the expression is ambiguous, pick the nearest future occurrence.
            """,
        name: "DateParser");

    var response = await agent.RunAsync(whenText);
    var responseText = response.ToString().Trim();

    if (!DateTime.TryParseExact(responseText, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var reminderTime))
    {
        Console.Error.WriteLine($"Could not parse date from model response: \"{responseText}\"");
        return 1;
    }

    var delay = reminderTime - DateTime.Now;

    if (delay.TotalSeconds <= 0)
    {
        Console.Error.WriteLine($"Time \"{reminderTime:yyyy-MM-dd HH:mm:ss}\" is in the past.");
        return 1;
    }

    var delaySeconds = (int)Math.Ceiling(delay.TotalSeconds);
    var unitName = $"remindme-{Guid.NewGuid():N}";

    var args_list = new List<string>
    {
        "--user",
        "--on-active=" + delaySeconds,
        $"--unit={unitName}",
        "--description=remindme: " + message,
    };

    // Pass through display env vars so notify-send can reach the desktop
    foreach (var envVar in new[] { "DISPLAY", "WAYLAND_DISPLAY", "DBUS_SESSION_BUS_ADDRESS" })
    {
        var val = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(val))
            args_list.Add($"--setenv={envVar}={val}");
    }

    args_list.AddRange(["notify-send", "--urgency=critical", "Reminder", message]);

    var psi = new ProcessStartInfo
    {
        FileName = "systemd-run",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    foreach (var arg in args_list)
        psi.ArgumentList.Add(arg);

    var process = Process.Start(psi)!;
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        var stderr = await process.StandardError.ReadToEndAsync();
        Console.Error.WriteLine($"Failed to schedule reminder: {stderr.Trim()}");
        return 1;
    }

    Console.WriteLine($"Reminder set for {reminderTime:yyyy-MM-dd HH:mm:ss} (in {FormatDelay(delay)}): {message}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static string FormatDelay(TimeSpan delay)
{
    if (delay.TotalSeconds < 60) return $"{(int)delay.TotalSeconds}s";
    if (delay.TotalMinutes < 60) return $"{(int)delay.TotalMinutes}m {delay.Seconds}s";
    if (delay.TotalHours < 24) return $"{(int)delay.TotalHours}h {delay.Minutes}m";
    return $"{(int)delay.TotalDays}d {delay.Hours}h {delay.Minutes}m";
}
