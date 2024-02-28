namespace Napoleon.Server.RequestReply;

public static class RequestConstants
{
    public static readonly int GetData = 10;
    public static readonly int PutData = 11;
    public static readonly int DeleteData = 12;

    /// <summary>
    /// A message that triggers an exception on the server (for tests only)
    /// </summary>
    public static readonly int RaiseExceptionForTests = 101;

    // internally used properties in the Json messages
    public static readonly string PropertyNameRequestType = "requestType";
    public static readonly string PropertyNameIsException = "isException";
    public static readonly string PropertyNameExceptionMessage = "exceptionMessage";

}