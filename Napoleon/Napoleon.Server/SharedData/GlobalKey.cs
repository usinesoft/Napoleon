namespace Napoleon.Server.SharedData;

/// <summary>
///     Uniquely identify an item in the store
/// </summary>
/// <param name="Collection"></param>
/// <param name="Key"></param>
public record GlobalKey(string Collection, string Key);