namespace Napoleon.Server.SharedData;

public interface IDataStore
{
    /// <summary>
    ///     Each modification of an item in the store increments this value
    /// </summary>
    int GlobalVersion { get; }

    public event EventHandler<DataChangedEventArgs> AfterDataChanged;

    /// <summary>
    ///     Individual changes can be applied only in order to guarantee data consistency.
    ///     They are usually received through an async channel that does not guarantee that the messages are delivered in-order
    /// </summary>
    /// <param name="change"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    bool TryApplyAsyncChange(Item change);

    /// <summary>
    ///     Apply an ordered sequence of changes. It comes through a channel that guarantees ordered delivery
    /// </summary>
    /// <param name="changes"></param>
    void ApplyChanges(IEnumerable<Item> changes);
}