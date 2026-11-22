using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>
/// What a <c>natss://</c> transfer needs to start TLS over the connection it opened in plain text
/// (ADR-1065): the standard NATS server sends its <c>INFO</c> line in plain text and waits for the
/// client to start TLS; a server with <c>handshake_first</c> sends nothing until it has the client's hello.
/// </summary>
/// <param name="TlsProvider">Runs the TLS handshake over the open connection.</param>
/// <param name="Host">The host name the server's certificate must match.</param>
/// <param name="Events">The transfer's events, which the handshake reports to.</param>
/// <param name="SilenceMeansHandshakeFirst">How long a server may stay silent before the client takes it for a <c>handshake_first</c> one and starts TLS.</param>
/// <param name="Time">The clock the silence is timed with.</param>
internal sealed record NatsTlsUpgrade(ITlsProvider TlsProvider, string Host, ITransferEvents Events, TimeSpan SilenceMeansHandshakeFirst, TimeProvider Time);
