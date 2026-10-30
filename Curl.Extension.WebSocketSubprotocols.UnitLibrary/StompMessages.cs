namespace Curl.Extension.WebSocketSubprotocols;

/// <summary>
/// STOMP 1.2's opening frames (https://stomp.github.io/stomp-specification-1.2.html): <c>CONNECT</c>, then, when a
/// destination is named, <c>SUBSCRIBE</c> to it with id <c>0</c>. Each frame ends with its NULL octet.
/// </summary>
internal static class StompMessages
{
    /// <summary>The subprotocol's registered name for STOMP 1.2 over WebSocket.</summary>
    internal const string Subprotocol = "v12.stomp";

    /// <summary>Gives <c>CONNECT</c> and, when <paramref name="destination" /> is not empty, <c>SUBSCRIBE</c>.</summary>
    /// <param name="destination">The destination to subscribe to; empty to only connect.</param>
    /// <param name="host">The <c>host</c> header's value, the URL's host.</param>
    /// <returns>The frames.</returns>
    internal static IReadOnlyList<string> Opening(string destination, string host)
    {
        // "Connecting": accept-version and host are required; CONNECT frames are not escaped ("Value Encoding").
        string connect = $"CONNECT\naccept-version:1.2\nhost:{host}\n\n\0";
        return destination.Length == 0
            ? [connect]
            : [connect, $"SUBSCRIBE\nid:0\ndestination:{Escape(destination)}\n\n\0"];
    }

    /// <summary>"Value Encoding": backslash, carriage return, line feed and colon become <c>\\</c>, <c>\r</c>, <c>\n</c> and <c>\c</c>.</summary>
    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace(":", "\\c", StringComparison.Ordinal);
}
