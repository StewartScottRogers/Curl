namespace Curl.Extended.Protocol.Xmpp.Client;

/// <summary>The error messages an XMPP transfer ends with (ADR-1068).</summary>
internal static class XmppClientMessages
{
    /// <summary>The connection ended where an element was due.</summary>
    internal const string Disconnected = "Connection disconnected";

    /// <summary>Sending to the server failed.</summary>
    internal const string SendFailed = "Failed sending XMPP data";

    /// <summary>The server sent XML that is not well formed.</summary>
    internal const string Malformed = "XMPP server sent malformed XML";

    /// <summary>The message or query holds a character XML 1.0 forbids.</summary>
    internal const string ForbiddenCharacter = "XMPP stanza holds a character XML 1.0 forbids";

    /// <summary>The URL's query names a query type the client does not provide.</summary>
    internal static string UnsupportedQuery(string queryType) => $"Unsupported XMPP query type: {queryType}";

    /// <summary>The disco query's <c>request</c> key names neither <c>info</c> nor <c>items</c>.</summary>
    internal static string UnsupportedDiscoRequest(string request) => $"Unsupported XMPP disco request: {request}";

    /// <summary>The server offers none of the SASL <paramref name="mechanisms"/> the client can log in with.</summary>
    internal static string MechanismNotOffered(IEnumerable<string> mechanisms) => $"XMPP server does not offer SASL {string.Join(" or ", mechanisms)}";

    /// <summary>The server refused the SASL authentication with <paramref name="condition"/>.</summary>
    internal static string AuthenticationFailed(string condition) => $"XMPP authentication failed: {condition}";

    /// <summary>The server sent a stream error with <paramref name="condition"/>.</summary>
    internal static string StreamError(string condition) => $"XMPP stream error: {condition}";

    /// <summary>The server answered a stanza with an error with <paramref name="condition"/>.</summary>
    internal static string StanzaError(string condition) => $"XMPP stanza error: {condition}";

    /// <summary>The server sent an element the exchange did not expect.</summary>
    internal static string UnexpectedElement(string name) => $"Unexpected XMPP element <{name}>";

    /// <summary>Writing <paramref name="length"/> bytes to the output failed.</summary>
    internal static string OutputWriteFailed(int length) => $"Failure writing output to destination, passed {length} returned 0";
}
