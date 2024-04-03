namespace Napoleon.Server.SharedData;

public class DataChangedEventArgs : EventArgs
{
    public DataChangedEventArgs(Item changedData)
    {
        ChangedData = changedData;
    }

    public Item ChangedData { get; }
}