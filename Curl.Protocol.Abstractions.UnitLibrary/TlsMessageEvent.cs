namespace Curl.Protocol.Abstractions;

/// <summary>
/// One TLS protocol message sent or received, as OpenSSL's message callback reports it,
/// reported through <see cref="ITransferEvents.ReportTlsMessage" /> (ADR-0085).
/// </summary>
/// <remarks>
/// curl's OpenSSL build writes an info line for each message
/// (<c>TLSv1.3 (OUT), TLS handshake, Client hello (1):</c>) and then the bytes as TLS data;
/// record headers and TLS 1.3 inner content types get only the bytes.
/// </remarks>
public sealed record TlsMessageEvent
{
    /// <summary>
    /// Gets the protocol version the message belongs to as OpenSSL numbers it, such as
    /// <c>0x0304</c> for TLS 1.3 or <c>0x0303</c> for TLS 1.2, or <c>0</c> when there is none yet.
    /// </summary>
    public required int ProtocolVersion { get; init; }

    /// <summary>
    /// Gets what kind of message <see cref="Bytes" /> holds.
    /// </summary>
    public required TlsContentType ContentType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the message was sent (<see langword="true" />) or
    /// received (<see langword="false" />).
    /// </summary>
    public required bool Sent { get; init; }

    /// <summary>
    /// Gets the message's bytes: for a handshake message its type byte first, for an alert
    /// its level and description.
    /// </summary>
    public required ReadOnlyMemory<byte> Bytes { get; init; }
}
