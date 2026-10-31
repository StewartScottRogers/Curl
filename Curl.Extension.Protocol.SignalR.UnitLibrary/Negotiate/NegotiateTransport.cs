namespace Curl.Extension.Protocol.SignalR.Negotiate;

/// <summary>One entry of a negotiate response's <c>availableTransports</c>: a transport and the formats it carries (TransportProtocols.md).</summary>
internal sealed record NegotiateTransport(string Transport, IReadOnlyList<string> TransferFormats);
