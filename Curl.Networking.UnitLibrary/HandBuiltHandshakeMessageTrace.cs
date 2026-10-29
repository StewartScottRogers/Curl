using System.Collections.Frozen;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Turns what a hand-built handshake's <see cref="TlsHandshakeMessageObserver" /> is told into
/// the OpenSSL build's <see cref="TlsMessageEvent" />s, which <c>-v</c> words as OpenSSL's
/// message callback trace (ADR-0982, BL-4009). OpenSSL's callback carries the connection's
/// version, not the record's: the highest version offered until the server answers, the
/// negotiated one from the server's first message on. That version is known only once the
/// handshake ends, so the messages are held until <see cref="ReportHeld" />, and every message
/// after it, such as a TLS 1.3 <c>NewSessionTicket</c>, is reported as it comes.
/// </summary>
/// <param name="events">Where the messages are reported.</param>
/// <param name="offeredVersion">The highest version the ClientHello offers, by its wire code point.</param>
internal sealed class HandBuiltHandshakeMessageTrace(ITransferEvents events, int offeredVersion)
{
    private static readonly FrozenDictionary<SslProtocols, int> WireVersions = new Dictionary<SslProtocols, int>
    {
        [SslProtocols.Tls13] = 0x0304,
        [SslProtocols.Tls12] = 0x0303,
        [TlsVersionRange.Tls11] = 0x0302,
        [TlsVersionRange.Tls10] = 0x0301,
    }.ToFrozenDictionary();

    private readonly List<(TlsMessageEvent Message, bool BeforeServerAnswered)> _held = [];

    private bool _serverAnswered;

    private int? _negotiatedVersion;

    /// <summary>Takes one message as <see cref="TlsHandshakeMessageObserver" /> is told it.</summary>
    /// <param name="direction">Whether the client sent or received it.</param>
    /// <param name="contentType">Handshake or change_cipher_spec.</param>
    /// <param name="message">The message's bytes.</param>
    internal void Observe(TlsTrafficDirection direction, Tls.TlsContentType contentType, ReadOnlySpan<byte> message)
    {
        var sent = direction == TlsTrafficDirection.Write;
        var beforeServerAnswered = sent && !_serverAnswered;
        _serverAnswered |= !sent;
        var reported = new TlsMessageEvent
        {
            ProtocolVersion = _negotiatedVersion ?? 0,
            ContentType = (Protocol.Abstractions.TlsContentType)(int)contentType,
            Sent = sent,
            Bytes = message.ToArray(),
        };
        if (_negotiatedVersion is null)
        {
            _held.Add((reported, beforeServerAnswered));
            return;
        }

        events.ReportTlsMessage(reported);
    }

    /// <summary>Reports the held messages with the version each carries, and every later one as it comes.</summary>
    /// <param name="negotiated">The version negotiated, or <see cref="SslProtocols.None" /> when none was.</param>
    internal void ReportHeld(SslProtocols negotiated)
    {
        var negotiatedVersion = WireVersions.TryGetValue(negotiated, out var wireVersion) ? wireVersion : offeredVersion;
        _negotiatedVersion = negotiatedVersion;
        foreach (var (message, beforeServerAnswered) in _held)
        {
            events.ReportTlsMessage(message with { ProtocolVersion = beforeServerAnswered ? offeredVersion : negotiatedVersion });
        }

        _held.Clear();
    }
}
