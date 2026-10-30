namespace Curl.Extension.WebSocketSubprotocols;

/// <summary>
/// The WAMP basic profile's opening messages in its JSON serialization (https://wamp-proto.org/wamp_bp_latest_ietf.html):
/// <c>HELLO</c> to join a realm as a subscriber, then, when a topic is named, <c>SUBSCRIBE</c> to it with request id <c>1</c>.
/// </summary>
internal static class WampMessages
{
    /// <summary>The subprotocol's name for the JSON serialization, "WebSocket Transport".</summary>
    internal const string Subprotocol = "wamp.2.json";

    /// <summary>The <c>HELLO</c> message type, "Message Codes and Direction".</summary>
    internal const int Hello = 1;

    /// <summary>The <c>SUBSCRIBE</c> message type, "Message Codes and Direction".</summary>
    internal const int Subscribe = 32;

    /// <summary>Gives <c>HELLO</c> for the realm and, when a topic follows it, <c>SUBSCRIBE</c> to the topic.</summary>
    /// <param name="argument">The realm's URI, then optionally the topic's.</param>
    /// <returns>The messages.</returns>
    /// <exception cref="FormatException">No realm, or more than a realm and a topic, was given.</exception>
    internal static IReadOnlyList<string> Opening(string argument)
    {
        string[] uris = argument.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return uris.Length switch
        {
            1 => [HelloFor(uris[0])],
            2 => [HelloFor(uris[0]), $"[{Subscribe},1,{{}},{SubprotocolJson.String(uris[1])}]"],
            _ => throw new FormatException($"--ws-subprotocol: {Subprotocol} needs a realm after its name, and may name one topic after the realm"),
        };
    }

    /// <summary>"Session Establishment": <c>[HELLO, Realm|uri, Details|dict]</c>, the details announcing the subscriber role.</summary>
    private static string HelloFor(string realm) => $"[{Hello},{SubprotocolJson.String(realm)},{{\"roles\":{{\"subscriber\":{{}}}}}}]";
}
