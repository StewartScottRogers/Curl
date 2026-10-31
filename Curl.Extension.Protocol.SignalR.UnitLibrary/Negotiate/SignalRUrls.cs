namespace Curl.Extension.Protocol.SignalR.Negotiate;

/// <summary>Builds the negotiate URL and the WebSocket URL of a SignalR endpoint (TransportProtocols.md).</summary>
internal static class SignalRUrls
{
    /// <summary>
    /// The negotiate URL: <c>/negotiate</c> appended to the endpoint's path and <c>negotiateVersion=1</c> to its query
    /// (TransportProtocols.md, "POST [endpoint-base]/negotiate request").
    /// </summary>
    public static Uri NegotiateUrl(Uri endpointUrl)
    {
        var builder = new UriBuilder(endpointUrl);
        builder.Path = builder.Path.TrimEnd('/') + "/negotiate";
        builder.Query = WithParameter(builder.Query, "negotiateVersion=1");
        return builder.Uri;
    }

    /// <summary>
    /// The WebSocket URL: the endpoint with <c>ws</c> for <c>http</c> and <c>wss</c> for <c>https</c>, and an <c>id</c>
    /// query parameter holding the connection token under version 1 or the connection id under version 0
    /// (TransportProtocols.md, "WebSockets (Full Duplex)").
    /// </summary>
    /// <exception cref="SignalRWireFormatException">The response names no connection for its version.</exception>
    public static Uri WebSocketUrl(Uri endpointUrl, NegotiateResponse response)
    {
        string id = (response.NegotiateVersion >= 1 ? response.ConnectionToken : response.ConnectionId)
            ?? throw new SignalRWireFormatException($"The negotiate response (version {response.NegotiateVersion}) names no connection.");
        var builder = new UriBuilder(endpointUrl)
        {
            Scheme = IsSecure(endpointUrl) ? "wss" : "ws",
        };
        builder.Query = WithParameter(builder.Query, "id=" + Uri.EscapeDataString(id));
        return builder.Uri;
    }

    private static bool IsSecure(Uri endpointUrl) =>
        endpointUrl.Scheme is "https" or "wss";

    private static string WithParameter(string query, string parameter) =>
        query.Length > 1 ? query + "&" + parameter : parameter;
}
