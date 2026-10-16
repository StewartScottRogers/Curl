using Curl.Cookies;
using Curl.Core.Hsts;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// What every easy handle of one <see cref="ManagedCurlEngine" /> shares, as the curl tool's transfers
/// share it across one run (ADR-0542): the connection, TLS session and DNS caches, the cookies, the HSTS
/// cache, and the <see cref="TransferPerformer" /> that numbers the transfers' <c>%{xfer_id}</c> and holds
/// curl's easy error text. None of it is static, so two engines in one process share nothing.
/// </summary>
/// <param name="timeProvider">The clock the caches, the HSTS cache and the transfers run on.</param>
/// <param name="easyErrorText">Curl's easy error text for each exit code (<c>Curl.Console</c>'s <c>CurlEasyErrorText.Of</c> in production).</param>
internal sealed class ManagedEngineSharedState(TimeProvider timeProvider, Func<CurlExitCode, string> easyErrorText)
{
    /// <summary>Gets the clock the engine's transfers run on.</summary>
    internal TimeProvider TimeProvider { get; } = timeProvider;

    /// <summary>Gets the pooled connections every handle's transfers connect through.</summary>
    internal ConnectionCache Connections { get; } = new(timeProvider);

    /// <summary>Gets the TLS sessions every handle's handshakes resume.</summary>
    internal TlsSessionCache TlsSessions { get; } = new(timeProvider);

    /// <summary>Gets the resolved names every handle's transfers look up first.</summary>
    internal DnsCache DnsCache { get; } = new();

    /// <summary>Gets the cookies every handle with the cookie engine on sends and keeps.</summary>
    internal CookieStore Cookies { get; } = new();

    /// <summary>Gets the HSTS cache every handle's transfers learn from and switch by.</summary>
    internal HstsTransferPolicy Hsts { get; } = new(timeProvider);

    /// <summary>Gets the performer that numbers the engine's transfers and holds curl's easy error text.</summary>
    internal TransferPerformer Performer { get; } = new(timeProvider, easyErrorText);
}
