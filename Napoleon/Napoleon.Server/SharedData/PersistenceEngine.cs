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

    public void SaveData(DataStore dataStore)
    {
        throw new NotImplementedException();
    }

    public void SaveChange(Item change, string? dataDirectory)
    {
        if (dataDirectory != null)
        {
            var json = JsonSerializer.Serialize(change, SerializationContext.Default.Item);

            var fileName = $"change{change.Version:D5}.json";

            File.WriteAllText(Path.Combine(dataDirectory, fileName), json);
        }
    }

    public void LoadData(DataStore dataStore, string dataDirectory)
    {
        _logger.LogInformation("Loading data from {dir}", dataDirectory);

        var dataPath = Path.Combine(dataDirectory, "data.json");
        if (File.Exists(dataPath))
        {
            var json = File.ReadAllText(dataPath);
            var jd = JsonSerializer.Deserialize(json, SerializationContext.Default.JsonDocument);
            if (jd != null) dataStore.DeserializeFromDocument(jd);
        }

        var changes = Directory.EnumerateFiles(dataDirectory, "change*.json").ToList();
        foreach (var change in changes)
        {
            _logger.LogInformation("Saving change {change}", change);

            var json = File.ReadAllText(change);
            var item = JsonSerializer.Deserialize(json, SerializationContext.Default.Item);
            if (item == null) throw new DataException($"Can not load {change}");
            dataStore.ApplyChanges(new List<Item> { item });
        }

        // save the file containing all changes and remove files containing individual changes

        _logger.LogInformation("Start compacting data files ");

        try
        {
            var doc = dataStore.SerializeToDocument();
            var jsonAll = JsonSerializer.Serialize(doc, SerializationContext.Default.JsonDocument);
            File.WriteAllText(dataPath, jsonAll);


            foreach (var change in changes) File.Delete(change);

            _logger.LogInformation("End compacting data files ");
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
        }
    }
}