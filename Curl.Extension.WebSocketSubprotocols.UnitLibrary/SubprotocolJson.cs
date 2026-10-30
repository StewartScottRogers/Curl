using System.Text.Encodings.Web;
using System.Text.Json;

namespace Curl.Extension.WebSocketSubprotocols;

/// <summary>Writes a JSON string literal (RFC 8259 §7) for the JSON messages graphql-transport-ws and WAMP send.</summary>
internal static class SubprotocolJson
{
    /// <summary>Gives <paramref name="text" /> as a quoted JSON string, escaping only what JSON requires escaped.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The JSON string literal.</returns>
    internal static string String(string text) => $"\"{JsonEncodedText.Encode(text, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).Value}\"";
}
