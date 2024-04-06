using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Napoleon.Server.SharedData;

public class PersistenceEngine : IPersistenceEngine
{
    private readonly ILogger<PersistenceEngine> _logger;

    public PersistenceEngine(ILogger<PersistenceEngine> logger)
    {
        _logger = logger;
    }

    public void SaveData(DataStore dataStore, string? dataDirectory)
    {
        if (dataDirectory == null) return;

        _logger.LogInformation("Saving full data");

        try
        {
            var dataPath = Path.Combine(dataDirectory, "data.json");

            var jDoc = dataStore.SerializeToDocument();

            var json = JsonSerializer.Serialize(jDoc, SerializationContext.Default.JsonDocument);

            File.WriteAllText(dataPath, json);
        }
        catch (Exception e)
        {
            _logger.LogError("Error saving full data:{msg}", e.Message);

            throw;
        }
    }

    public void SaveChange(Item change, string? dataDirectory)
    {
        if (dataDirectory == null) return;

        _logger.LogDebug("Saving change {change}", change.Version);

        try
        {
            var json = JsonSerializer.Serialize(change, SerializationContext.Default.Item);

            var fileName = $"change{change.Version:D5}.json";

            File.WriteAllText(Path.Combine(dataDirectory, fileName), json);
        }
        catch (Exception e)
        {
            _logger.LogError("Error saving change {change}:{msg}", change.Version, e.Message);
            throw;
        }
    }

    public void LoadData(DataStore dataStore, string? dataDirectory)
    {
        if (dataDirectory == null) return;

        _logger.LogInformation("Loading data from {dir}", dataDirectory);

        List<string> changes;
        try
        {
            var dataPath = Path.Combine(dataDirectory, "data.json");
            if (File.Exists(dataPath))
            {
                var json = File.ReadAllText(dataPath);
                var jd = JsonSerializer.Deserialize(json, SerializationContext.Default.JsonDocument);
                if (jd != null) dataStore.DeserializeFromDocument(jd);
            }

            changes = Directory.EnumerateFiles(dataDirectory, "change*.json").ToList();
            foreach (var change in changes)
            {
                _logger.LogInformation("Saving change {change}", change);

                var json = File.ReadAllText(change);
                var item = JsonSerializer.Deserialize(json, SerializationContext.Default.Item);
                if (item == null) throw new DataException($"Can not load {change}");
                dataStore.ApplyChanges(new List<Item> { item });
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Error when applying individual changes:{err}", e.Message);
            throw;
        }

        // save the file containing all changes and remove files containing individual changes

        _logger.LogInformation("Start compacting data files ");

        try
        {
            SaveData(dataStore, dataDirectory);


            foreach (var change in changes) File.Delete(change);

            _logger.LogInformation("End compacting data files ");
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
        }
    }
}