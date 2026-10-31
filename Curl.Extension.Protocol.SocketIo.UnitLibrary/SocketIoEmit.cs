using System.Text;
using System.Text.Json;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.SocketIo;

/// <summary>Reads the event a transfer emits from its <c>-d</c> data: a JSON array whose first element is the event name.</summary>
internal static class SocketIoEmit
{
    /// <summary>The message a transfer fails with, exit 2, when its data is not such an array.</summary>
    public const string NotAnEventMessage = "Socket.IO: the data to emit must be a JSON array whose first element is the event name";

    /// <summary>Gives the event's JSON array as text, or <see langword="null" /> when the transfer has no data.</summary>
    /// <param name="postData">The transfer's <c>-d</c> data.</param>
    /// <returns>The array's text.</returns>
    /// <exception cref="SocketIoException">The data is not a JSON array starting with a string.</exception>
    public static string? From(ReadOnlyMemory<byte>? postData)
    {
        if (postData is not { } data)
        {
            return null;
        }

        string text = Encoding.UTF8.GetString(data.Span);
        bool isEvent;
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            isEvent = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.String;
        }
        catch (JsonException)
        {
            isEvent = false;
        }

        return isEvent ? text : throw new SocketIoException(CurlExitCode.FailedInit, NotAnEventMessage);
    }
}
