namespace Curl.Extended.Protocol.SignalR.Transport;

/// <summary>Where a negotiate exchange sent the client: the WebSocket URL to open and the bearer token to open it with.</summary>
/// <param name="WebSocketUrl">The <c>ws</c> or <c>wss</c> URL with the connection's <c>id</c> (TransportProtocols.md).</param>
/// <param name="AccessToken">The access token a negotiate redirect gave, or <see langword="null"/> without a redirect.</param>
/// <param name="RequestFields">The option fields the upgrade request may carry: those the last negotiate request carried
/// (BL-5311: a redirect to another origin drops the <c>Authorization</c> of <c>-u</c>, <c>--oauth2-bearer</c> and <c>-H</c>).</param>
internal sealed record NegotiatedWebSocketEndpoint(Uri WebSocketUrl, string? AccessToken, IReadOnlyList<KeyValuePair<string, string>>? RequestFields);
