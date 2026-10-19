namespace Curl.Networking;

/// <summary>
/// One session <see cref="TlsSessionCache" /> holds, as curl 8.22.0's
/// <c>Curl_ssl_session_export</c> hands it to a <c>curl_easy_ssls_export</c> callback.
/// </summary>
/// <param name="PeerKey">The peer key the session is held under, or <see langword="null" /> for a peer known only by its salt and hmac.</param>
/// <param name="SaltAndHmac">The 32-byte salt then the 32-byte HMAC-SHA256 of the peer key under it.</param>
/// <param name="PackedSession">The session packed as <c>vtls_spack.c</c> packs it.</param>
/// <param name="ValidUntil">When the session expires, in seconds since the Unix epoch.</param>
/// <param name="ProtocolId">The IETF version the session was made with, <c>0x0304</c> for TLS 1.3.</param>
/// <param name="ApplicationProtocol">The ALPN protocol the session was made with, or <see langword="null" />.</param>
/// <param name="MaxEarlyData">The most 0-RTT data the session allows, in bytes; zero for none.</param>
public sealed record HeldTlsSession(
    string? PeerKey,
    byte[] SaltAndHmac,
    byte[] PackedSession,
    long ValidUntil,
    int ProtocolId,
    string? ApplicationProtocol,
    uint MaxEarlyData);
