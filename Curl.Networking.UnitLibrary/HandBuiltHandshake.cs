using System.Security.Authentication;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// How a hand-built handshake ended: the connected stream and what was negotiated, or why
/// it failed.
/// </summary>
/// <param name="Stream">The connected stream, or <see langword="null" /> when the handshake failed.</param>
/// <param name="ProtocolVersion">The version negotiated.</param>
/// <param name="CipherSuite">The suite negotiated, by its code point.</param>
/// <param name="ApplicationProtocol">The protocol the server selected through ALPN, or <see langword="null" />.</param>
/// <param name="Failure">Why the handshake failed, or <see langword="null" /> when it completed.</param>
internal sealed record HandBuiltHandshake(
    Stream? Stream,
    SslProtocols ProtocolVersion,
    ushort CipherSuite,
    string? ApplicationProtocol,
    TlsHandshakeFailure? Failure)
{
    /// <summary>Describes a completed TLS 1.3 handshake.</summary>
    /// <param name="stream">The connected stream.</param>
    /// <returns>The outcome.</returns>
    internal static HandBuiltHandshake Completed(Tls13ClientStream stream) =>
        new(stream, SslProtocols.Tls13, stream.Handshake.CipherSuite!.Code, stream.Handshake.ApplicationProtocol, null);

    /// <summary>Describes a completed TLS 1.2, 1.1 or 1.0 handshake.</summary>
    /// <param name="stream">The connected stream.</param>
    /// <returns>The outcome.</returns>
    internal static HandBuiltHandshake Completed(Tls12ClientStream stream) =>
        new(
            stream,
            ToSslProtocols(stream.Handshake.Version!.Value),
            stream.Handshake.CipherSuite!.Code,
            stream.Handshake.ApplicationProtocol,
            null);

    /// <summary>Describes a failed handshake.</summary>
    /// <param name="failure">Why it failed.</param>
    /// <returns>The outcome.</returns>
    internal static HandBuiltHandshake Failed(TlsHandshakeFailure failure) =>
        new(null, SslProtocols.None, 0, null, failure);

    /// <summary>Names a TLS 1.2-and-below version as <see cref="SslProtocols" /> does, for the handshake event.</summary>
    /// <param name="version">The version negotiated.</param>
    /// <returns>The same version as an <see cref="SslProtocols" /> value.</returns>
    internal static SslProtocols ToSslProtocols(TlsProtocolVersion version) => version switch
    {
        TlsProtocolVersion.Tls10 => TlsVersionRange.Tls10,
        TlsProtocolVersion.Tls11 => TlsVersionRange.Tls11,
        _ => SslProtocols.Tls12,
    };
}
