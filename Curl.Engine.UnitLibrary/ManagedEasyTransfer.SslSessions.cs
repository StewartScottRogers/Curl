using Curl.Api.Codes;
using Curl.Api.EngineContracts.Tls;
using Curl.Networking;

namespace Curl.Engine;

// curl_easy_ssls_import and curl_easy_ssls_export (BL-2786): over the attached share handle's TLS session cache
// while it shares CURL_LOCK_DATA_SSL_SESSION, else the engine's, and only in a build listing SSLS-EXPORT.
internal sealed partial class ManagedEasyTransfer
{
    /// <summary>
    /// Gets or sets whether the build has <c>SSLS-EXPORT</c>: the OpenSSL build Curl matches off Windows does, the
    /// Schannel build it matches on Windows does not, and there both calls answer <see cref="CurlCode.NotBuiltIn" />.
    /// </summary>
    internal bool ExportsTlsSessions { get; set; } = !OperatingSystem.IsWindows();

    /// <inheritdoc />
    public CurlCode ImportTlsSession(string? sessionKey, byte[]? saltAndHmac, byte[] packedSession)
    {
        ArgumentNullException.ThrowIfNull(packedSession);
        return ExportsTlsSessions
            ? WithTlsSessions(cache => (CurlCode)cache.ImportSession(sessionKey, saltAndHmac, packedSession))
            : CurlCode.NotBuiltIn;
    }

    /// <inheritdoc />
    public CurlCode ExportTlsSessions(Func<ExportedTlsSession, CurlCode> export)
    {
        ArgumentNullException.ThrowIfNull(export);
        return ExportsTlsSessions ? WithTlsSessions(cache => Export(cache.ListSessions(), export)) : CurlCode.NotBuiltIn;
    }

    // Curl_ssl_session_export: each session in turn, stopping at the callback's first answer other than OK.
    private static CurlCode Export(IReadOnlyList<HeldTlsSession> sessions, Func<ExportedTlsSession, CurlCode> export)
    {
        foreach (HeldTlsSession held in sessions)
        {
            CurlCode code = export(new ExportedTlsSession(
                held.PeerKey,
                held.SaltAndHmac,
                held.PackedSession,
                held.ValidUntil,
                held.ProtocolId,
                held.ApplicationProtocol,
                held.MaxEarlyData));
            if (code != CurlCode.Ok)
            {
                return code;
            }
        }

        return CurlCode.Ok;
    }

    private CurlCode WithTlsSessions(Func<TlsSessionCache, CurlCode> use) =>
        optionShare is { } share ? share.WithTlsSessions(this, shared.TlsSessions, use) : use(shared.TlsSessions);
}
