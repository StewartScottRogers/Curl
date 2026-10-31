using Curl.Http2;

namespace Curl.Extension.WebSocketExtendedConnect.Wire;

/// <summary>
/// Builds the field list of the extended CONNECT request that opens a WebSocket: RFC 8441 section 4 on HTTP/2 and
/// RFC 9220 section 3 on HTTP/3, which use the same fields.
/// </summary>
internal static class ExtendedConnectRequestFields
{
    /// <summary>The <c>:protocol</c> value that asks for a WebSocket (RFC 8441 section 4).</summary>
    internal const string WebSocketProtocol = "websocket";

    /// <summary>The only WebSocket version the request offers (RFC 8441 section 5, RFC 6455 section 4.1).</summary>
    internal const string WebSocketVersion = "13";

    /// <summary>
    /// Gives the request's fields: <c>:method CONNECT</c>, <c>:protocol websocket</c>, <c>:scheme</c>, <c>:path</c>
    /// and <c>:authority</c>, then <c>sec-websocket-version: 13</c> and, when <paramref name="subprotocols" /> is not
    /// empty, <c>sec-websocket-protocol</c> listing them comma-separated. No <c>sec-websocket-key</c>: RFC 8441
    /// section 5 drops it.
    /// </summary>
    /// <param name="scheme">The <c>:scheme</c>: <c>https</c> for wss, <c>http</c> for ws.</param>
    /// <param name="path">The <c>:path</c>, the request path with any query.</param>
    /// <param name="authority">The <c>:authority</c>, the host with any non-default port.</param>
    /// <param name="subprotocols">The WebSocket subprotocols the client offers, in order of preference.</param>
    /// <returns>The fields, pseudo-header fields first.</returns>
    internal static IReadOnlyList<HeaderField> Build(string scheme, string path, string authority, IReadOnlyList<string> subprotocols)
    {
        List<HeaderField> fields =
        [
            new(":method", "CONNECT"),
            new(":protocol", WebSocketProtocol),
            new(":scheme", scheme),
            new(":path", path),
            new(":authority", authority),
            new("sec-websocket-version", WebSocketVersion),
        ];
        if (subprotocols.Count > 0)
        {
            fields.Add(new("sec-websocket-protocol", string.Join(", ", subprotocols)));
        }

        return fields;
    }
}
