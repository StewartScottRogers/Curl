using System.Collections.Concurrent;
using Curl.Cookies;
using Curl.Core.Hsts;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// What every easy handle of one <see cref="ManagedCurlEngine" /> shares, as the curl tool's transfers
/// share it across one run (ADR-0542): the connection, TLS session and DNS caches, the cookies, the HSTS
/// cache, and the <see cref="TransferPerformer" /> that numbers the transfers' <c>%{xfer_id}</c> and holds
/// curl's easy error text. None of it is static, so two engines in one process share nothing, except through
/// a <see cref="ManagedShareHandle" />, whose caches a transfer attached to it runs over (ADR-0606).
/// </summary>
internal sealed class ManagedEngineSharedState
{
    /// <summary>Creates an engine's state, every cache its own.</summary>
    /// <param name="timeProvider">The clock the caches, the HSTS cache and the transfers run on.</param>
    /// <param name="easyErrorText">Curl's easy error text for each exit code (<c>Curl.Console</c>'s <c>CurlEasyErrorText.Of</c> in production).</param>
    internal ManagedEngineSharedState(TimeProvider timeProvider, Func<CurlExitCode, string> easyErrorText)
    {
        TimeProvider = timeProvider;
        Connections = new ConnectionCache(timeProvider);
        TlsSessions = new TlsSessionCache(timeProvider);
        DnsCache = new DnsCache();
        Cookies = new CookieStore();
        Hsts = new HstsTransferPolicy(timeProvider);
        ConnectionIdsByPoolNumber = new ConcurrentDictionary<long, long>();
        MultiOwners = new ConcurrentDictionary<Api.EngineContracts.IEasyTransfer, ManagedMultiTransfer>(ReferenceEqualityComparer.Instance);
        SocketHandles = new CurlSocketHandles();
        Performer = new TransferPerformer(timeProvider, easyErrorText);
    }

    /// <summary>
    /// Creates the state one perform of a transfer attached to a share handle runs over (<see cref="ManagedShareHandle.Attach" />):
    /// <paramref name="engine" />'s clock, numbering, multi owners and performer, with the caches given.
    /// </summary>
    /// <param name="engine">The transfer's engine's state.</param>
    /// <param name="connections">The pooled connections.</param>
    /// <param name="tlsSessions">The TLS sessions.</param>
    /// <param name="dnsCache">The resolved names.</param>
    /// <param name="cookies">The cookies.</param>
    /// <param name="hsts">The HSTS cache.</param>
    internal ManagedEngineSharedState(
        ManagedEngineSharedState engine,
        ConnectionCache connections,
        TlsSessionCache tlsSessions,
        DnsCache dnsCache,
        CookieStore cookies,
        HstsTransferPolicy hsts)
    {
        TimeProvider = engine.TimeProvider;
        Connections = connections;
        TlsSessions = tlsSessions;
        DnsCache = dnsCache;
        Cookies = cookies;
        Hsts = hsts;
        ConnectionIdsByPoolNumber = engine.ConnectionIdsByPoolNumber;
        MultiOwners = engine.MultiOwners;
        SocketHandles = engine.SocketHandles;
        Performer = engine.Performer;
    }

    /// <summary>Gets the clock the engine's transfers run on.</summary>
    internal TimeProvider TimeProvider { get; }

    /// <summary>Gets the pooled connections every handle's transfers connect through.</summary>
    internal ConnectionCache Connections { get; }

    /// <summary>Gets the TLS sessions every handle's handshakes resume.</summary>
    internal TlsSessionCache TlsSessions { get; }

    /// <summary>Gets the resolved names every handle's transfers look up first.</summary>
    internal DnsCache DnsCache { get; }

    /// <summary>Gets the cookies every handle with the cookie engine on sends and keeps.</summary>
    internal CookieStore Cookies { get; }

    /// <summary>Gets the HSTS cache every handle's transfers learn from and switch by.</summary>
    internal HstsTransferPolicy Hsts { get; }

    /// <summary>Gets the <c>%{conn_id}</c> of each connection a handle's transfer opened, by the pool's number for it, which a later transfer reusing it takes (<see cref="EngineConnectionIdTransferEvents" />).</summary>
    internal ConcurrentDictionary<long, long> ConnectionIdsByPoolNumber { get; }

    /// <summary>Gets the multi handle each transfer added to one of the engine's multi handles is in, so it is in one at a time (<see cref="ManagedMultiTransfer" />).</summary>
    internal ConcurrentDictionary<Api.EngineContracts.IEasyTransfer, ManagedMultiTransfer> MultiOwners { get; }

    /// <summary>Gets the opaque socket handles the engine gives its connections when no open-socket callback made one (<see cref="SocketCallbackConnector" />).</summary>
    internal CurlSocketHandles SocketHandles { get; }

    /// <summary>Gets the performer that numbers the engine's transfers and connections and holds curl's easy error text.</summary>
    internal TransferPerformer Performer { get; }
}
