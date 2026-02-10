using AiCoreUtils.Common;

try
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: select-model <number>");
        return 1;
    }

    if (!int.TryParse(args[0], out var selection) || selection < 1)
    {
        Console.Error.WriteLine("Please provide a valid model number from list-models.");
        return 1;
    }

    var models = await ModelService.ListModelsAsync();

    if (selection > models.Count)
    {
        Console.Error.WriteLine($"Invalid selection. Only {models.Count} model(s) available.");
        return 1;
    }

    var model = models[selection - 1];

    Console.WriteLine($"Selected: {model}");
    ConfigManager.WriteModel(model);
    Console.WriteLine("Saved to config.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
