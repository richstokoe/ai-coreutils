using AiCoreUtils.Common;

try
{
    var models = await ModelService.ListModelsAsync();

    if (models.Count == 0)
    {
        Console.Error.WriteLine("No models found.");
        return 1;
    }

    for (var i = 0; i < models.Count; i++)
    {
        Console.WriteLine($"  {i + 1}. {models[i]}");
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
