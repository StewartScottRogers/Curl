namespace Curl.Extension.WebSocketSubprotocols;

/// <summary>
/// What a <c>--ws-subprotocol</c> value asks for: the subprotocol's name, the <c>Sec-WebSocket-Protocol</c> token,
/// and its opening messages, in the order they are sent.
/// </summary>
/// <param name="Name">The subprotocol's registered name.</param>
/// <param name="Messages">The opening messages, each sent as one text frame.</param>
internal sealed record WebSocketSubprotocolRequest(string Name, IReadOnlyList<string> Messages)
{
    /// <summary>Reads a <c>--ws-subprotocol</c> value: the subprotocol's name, then what its messages carry.</summary>
    /// <param name="value">The value, <c>&lt;name&gt; [argument]</c>.</param>
    /// <param name="host">The URL's host, which a STOMP <c>CONNECT</c> frame names.</param>
    /// <returns>The subprotocol and its opening messages.</returns>
    /// <exception cref="FormatException">The value names no subprotocol this extension speaks, or not what it needs.</exception>
    internal static WebSocketSubprotocolRequest Parse(string value, string host)
    {
        string[] words = value.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string name = words.Length > 0 ? words[0] : "";
        string argument = words.Length > 1 ? words[1] : "";
        IReadOnlyList<string> messages = name switch
        {
            GraphQlTransportWsMessages.Subprotocol => GraphQlTransportWsMessages.Opening(argument),
            WampMessages.Subprotocol => WampMessages.Opening(argument),
            StompMessages.Subprotocol => StompMessages.Opening(argument, host),
            _ => throw new FormatException(
                $"--ws-subprotocol: '{name}' is not a subprotocol this extension speaks; use {GraphQlTransportWsMessages.Subprotocol}, {WampMessages.Subprotocol} or {StompMessages.Subprotocol}"),
        };
        return new(name, messages);
    }
}
