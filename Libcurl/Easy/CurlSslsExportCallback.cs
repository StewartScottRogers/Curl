using Curl.Api.Codes;

namespace Libcurl.Easy;

/// <summary>
/// <c>curl_ssls_export_cb</c>: given one TLS session by <see cref="CurlEasy.SslsExport"/>. Its closure is libcurl's
/// <c>userptr</c>, so the delegate takes none.
/// </summary>
/// <param name="handle">The handle being exported from.</param>
/// <param name="sessionKey">The peer key the session is held under, or <see langword="null"/> for a peer known only by its salt and hmac.</param>
/// <param name="saltAndHmac">The 64-byte <c>shmac</c>: a 32-byte salt then the HMAC-SHA256 of the peer key under it.</param>
/// <param name="packedSession">The session packed as <see cref="CurlEasy.SslsImport"/> takes it back (<c>sdata</c>).</param>
/// <param name="validUntil">When the session expires, in seconds since the Unix epoch.</param>
/// <param name="ietfTlsId">The IETF version the session was made with, <c>0x0304</c> for TLS 1.3.</param>
/// <param name="alpn">The ALPN protocol the session was made with, or <see langword="null"/>.</param>
/// <param name="earlyDataMax">The most 0-RTT data the session allows, in bytes; zero for none.</param>
/// <returns><see cref="CurlCode.Ok"/> to go on to the next session; any other code stops the export with it.</returns>
public delegate CurlCode CurlSslsExportCallback(
    CurlEasy handle,
    string? sessionKey,
    byte[] saltAndHmac,
    byte[] packedSession,
    long validUntil,
    int ietfTlsId,
    string? alpn,
    uint earlyDataMax);
