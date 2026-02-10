using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AiCoreUtils.Common;

public static class ConfigManager
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ai-coreutils");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public static IConfiguration Load()
    {
        return new ConfigurationBuilder()
            .AddJsonFile(ConfigPath, optional: true)
            .AddEnvironmentVariables("AICOREUTILS_")
            .Build();
    }

    public static string GetEndpoint()
    {
        return Load()["Endpoint"] ?? "http://localhost:1234/v1";
    }

    public static string GetModel()
    {
        return Load()["Model"] ?? "default";
    }

    public static void WriteModel(string model)
    {
        Dictionary<string, string> existing = [];

        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            existing = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }

        existing["Model"] = model;

        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(existing, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}
