using Curl.Api.Codes;
using Curl.Api.EngineContracts.Tls;

namespace Libcurl.Easy;

// curl_easy_ssls_import and curl_easy_ssls_export (BL-2669): straight calls to the engine's IEasyTransfer, which
// works on the share handle's TLS session cache while it shares CURL_LOCK_DATA_SSL_SESSION, else its own, and
// answers NotBuiltIn where the build lists no SSLS-EXPORT.
public sealed partial class CurlEasy
{
    /// <summary>
    /// <c>curl_easy_ssls_import</c>: adds one session, packed as curl 8.22.0's <c>lib/vtls/vtls_spack.c</c> packs it, to
    /// the handle's TLS session cache under <paramref name="sessionKey"/>, or under the peer whose salt and hmac
    /// <paramref name="saltAndHmac"/> holds when the key is <see langword="null"/>.
    /// </summary>
    /// <param name="sessionKey">The peer key, such as <c>example.test:443</c>, or <see langword="null"/>.</param>
    /// <param name="saltAndHmac">The 64-byte <c>shmac</c>, read when <paramref name="sessionKey"/> is <see langword="null"/>; <see langword="null"/> for none.</param>
    /// <param name="packedSession">The packed session (<c>sdata</c>); <see langword="null"/> is an empty one.</param>
    /// <returns>
    /// The engine's answer: <see cref="CurlCode.Ok"/>, <see cref="CurlCode.ReadError"/> for a packet that does not unpack,
    /// <see cref="CurlCode.BadFunctionArgument"/> for neither a key nor a 64-byte salt and hmac, or
    /// <see cref="CurlCode.NotBuiltIn"/> where the build has no <c>SSLS-EXPORT</c>.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlCode SslsImport(string? sessionKey, byte[]? saltAndHmac, byte[]? packedSession) =>
        LiveTransfer().ImportTlsSession(sessionKey, saltAndHmac, packedSession ?? []);

    /// <summary>
    /// <c>curl_easy_ssls_export</c>: hands every session the handle's TLS session cache holds to
    /// <paramref name="export"/>, one call each, stopping at its first answer other than <see cref="CurlCode.Ok"/>.
    /// </summary>
    /// <param name="export">The <c>curl_ssls_export_cb</c>; its closure is libcurl's <c>userptr</c>.</param>
    /// <returns>
    /// <see cref="CurlCode.Ok"/>, the callback's first other answer, <see cref="CurlCode.NotBuiltIn"/> where the build has
    /// no <c>SSLS-EXPORT</c>, or <see cref="CurlCode.BadFunctionArgument"/> for a <see langword="null"/> callback,
    /// without the engine, as libcurl's <c>Curl_ssl_session_export</c> refuses a missing <c>export_fn</c>.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlCode SslsExport(CurlSslsExportCallback? export)
    {
        var live = LiveTransfer();
        return export is null
            ? CurlCode.BadFunctionArgument
            : live.ExportTlsSessions(session => export(
                this,
                session.SessionKey,
                session.SaltAndHmac,
                session.PackedSession,
                session.ValidUntil,
                session.IetfTlsId,
                session.Alpn,
                session.MaxEarlyData));
    }
}
