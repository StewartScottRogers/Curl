namespace Curl.Extended.Protocol.SignalR.Negotiate;

/// <summary>
/// The body of the answer to <c>POST [endpoint-base]/negotiate</c> (TransportProtocols.md, "POST [endpoint-base]/negotiate
/// request"): a connection to open, a redirect to another endpoint, or an error.
/// </summary>
/// <param name="NegotiateVersion">The version the server answered with; 0 when it sent none.</param>
/// <param name="ConnectionId">The connection's id, the transports' <c>id</c> under version 0.</param>
/// <param name="ConnectionToken">The connection's token, the transports' <c>id</c> under version 1.</param>
/// <param name="AvailableTransports">The transports the server offers, in its order.</param>
/// <param name="Url">The endpoint to negotiate with instead, in the redirect form.</param>
/// <param name="AccessToken">The bearer token to send to <paramref name="Url"/>, in the redirect form.</param>
/// <param name="Error">Why the server refused the connection.</param>
internal sealed record NegotiateResponse(
    int NegotiateVersion,
    string? ConnectionId,
    string? ConnectionToken,
    IReadOnlyList<NegotiateTransport> AvailableTransports,
    string? Url,
    string? AccessToken,
    string? Error)
{
    /// <summary>Whether the server sent the client to another endpoint (<see cref="Url"/>).</summary>
    public bool IsRedirect => Url is not null;

    /// <summary>Whether the server offers the WebSockets transport.</summary>
    public bool OffersWebSockets => AvailableTransports.Any(transport => transport.Transport == "WebSockets");
}
