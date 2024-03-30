using static System.Formats.Asn1.AsnWriter;

namespace Napoleon.Server.SharedData;

public interface IPersistenceEngine
{
    public void SaveData(DataStore dataStore);
    public void SaveChange(Item change);
    public void LoadData(DataStore dataStore, string dataDirectory);

}