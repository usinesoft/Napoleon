namespace Napoleon.Server.RequestReply;

public static class RequestConstants
{
    /// <summary>
    ///     By convention simple request types (one request, one response) have values less than 1000
    /// </summary>
    public static readonly int GetData = 10;

    public static readonly int PutData = 11;
    public static readonly int DeleteData = 12;
    public static readonly int GetClusterStatus = 13;

    /// <summary>
    ///     A message that triggers an exception on the server (for tests only)
    /// </summary>
    public static readonly int RaiseExceptionForTests = 101;

    public static readonly int StreamingRequestTypesStartAt = 1000;

    /// <summary>
    ///     By convention streaming requests (une request, IAsyncEnumerable as response) have values greater than 1000
    /// </summary>
    public static readonly int StreamChanges = 1001;


    // Internally used properties in the Json messages
    public static readonly string PropertyNameRequestType = "requestType";
    public static readonly string PropertyNameIsException = "isException";
    public static readonly string PropertyNameExceptionMessage = "exceptionMessage";
}