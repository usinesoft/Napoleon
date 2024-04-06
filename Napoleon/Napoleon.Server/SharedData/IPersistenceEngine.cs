namespace Napoleon.Server.SharedData;

public interface IPersistenceEngine
{
    public void SaveChange(Item change, string? dataDirectory);
    public void LoadData(DataStore dataStore, string? dataDirectory);
}