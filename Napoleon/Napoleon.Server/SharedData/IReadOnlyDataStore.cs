using System.Text.Json;

namespace Napoleon.Server.SharedData;

public interface IReadOnlyDataStore
{
    /// <summary>
    ///     Returns the value as <see cref="JsonElement" />. If the value is not fount the returned JSonElement has ValueKind =
    ///     Undefined
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    JsonElement TryGetValue(string collection, string key);

    /// <summary>
    ///     Get as typed object. Null is returned if not found
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    T? TryGetValue<T>(string collection, string key) where T : class;

    /// <summary>
    ///     Return a typed simple value. As the default value can be a real value the boolean
    /// should be checked to know if the value was found
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    Tuple<T, bool> TryGetScalarValue<T>(string collection, string key) where T : struct;
}