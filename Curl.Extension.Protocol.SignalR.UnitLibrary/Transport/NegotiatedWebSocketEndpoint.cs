namespace Curl.Extension.Protocol.SignalR.Transport;

/// <summary>Where a negotiate exchange sent the client: the WebSocket URL to open and the bearer token to open it with.</summary>
/// <param name="WebSocketUrl">The <c>ws</c> or <c>wss</c> URL with the connection's <c>id</c> (TransportProtocols.md).</param>
/// <param name="AccessToken">The access token a negotiate redirect gave, or <see langword="null"/> without a redirect.</param>
internal sealed record NegotiatedWebSocketEndpoint(Uri WebSocketUrl, string? AccessToken);
