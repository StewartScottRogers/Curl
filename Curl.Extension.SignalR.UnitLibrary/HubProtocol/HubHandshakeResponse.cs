namespace Curl.Extension.SignalR.HubProtocol;

/// <summary>The server's answer to the handshake request: no <paramref name="Error"/> when it accepted the protocol (HubProtocol.md, "Overview").</summary>
internal sealed record HubHandshakeResponse(string? Error);
