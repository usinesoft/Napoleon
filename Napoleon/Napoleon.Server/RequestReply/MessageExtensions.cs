using System.Text.Json;

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
}