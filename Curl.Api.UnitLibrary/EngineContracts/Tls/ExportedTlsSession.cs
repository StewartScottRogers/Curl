namespace Curl.Api.EngineContracts.Tls;

/// <summary>
/// One TLS session <see cref="IEasyTransfer.ExportTlsSessions" /> hands its callback, the arguments curl 8.22.0's
/// <c>curl_ssls_export_cb</c> receives.
/// </summary>
/// <param name="SessionKey">The peer key the session is held under, or <see langword="null" /> for a peer known only by its salt and hmac.</param>
/// <param name="SaltAndHmac">The 32-byte salt then the 32-byte HMAC-SHA256 of the peer key under it (<c>shmac</c>).</param>
/// <param name="PackedSession">The session packed as libcurl packs it (<c>sdata</c>).</param>
/// <param name="ValidUntil">When the session expires, in seconds since the Unix epoch.</param>
/// <param name="IetfTlsId">The IETF version the session was made with, <c>0x0304</c> for TLS 1.3.</param>
/// <param name="Alpn">The ALPN protocol the session was made with, or <see langword="null" />.</param>
/// <param name="MaxEarlyData">The most 0-RTT data the session allows, in bytes; zero for none.</param>
public sealed record ExportedTlsSession(
    string? SessionKey,
    byte[] SaltAndHmac,
    byte[] PackedSession,
    long ValidUntil,
    int IetfTlsId,
    string? Alpn,
    uint MaxEarlyData);
