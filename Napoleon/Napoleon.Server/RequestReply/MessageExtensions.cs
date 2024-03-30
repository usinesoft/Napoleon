using System.Text.Json;
using System.Text.Json.Nodes;
using Napoleon.Server.SharedData;

namespace Napoleon.Server.RequestReply;

public static class MessageExtensions
{
    public static void ThrowIfExceptionResponse(this JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object)
            return;

        // process exception responses
        if (response.TryGetProperty(RequestConstants.PropertyNameIsException, out var value) && value.GetBoolean())
        {
            var message = response.GetProperty(RequestConstants.PropertyNameExceptionMessage).GetString() ??
                          "exception returned without a message";
            throw new NotSupportedException(message);
        }
    }

    public static string ToExceptionResponseJson(this Exception exception)
    {
        var exceptionResponse = new JsonObject
        {
            [RequestConstants.PropertyNameIsException] = true,
            [RequestConstants.PropertyNameExceptionMessage] = exception.Message
        };

        return JsonSerializer.Serialize(exceptionResponse, SerializationContext.Default.JsonObject);
    }
}