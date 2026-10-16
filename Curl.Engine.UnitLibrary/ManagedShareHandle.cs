using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Share;
using Curl.Cookies;
using Curl.Core.Hsts;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The engine's <see cref="IShareHandle" /> (<c>curl_share_init</c>, ADR-0606): its own cookie store, DNS cache,
/// TLS session cache, connection cache and HSTS cache, which a transfer whose <c>CURLOPT_SHARE</c> names it uses in
/// place of its engine's for each kind <see cref="Share" /> turned on (<see cref="Attach" />), so transfers of any
/// engine attached to one share handle send each other's cookies and reuse each other's connections. The lock
/// callbacks are called around each access: <see cref="CurlLockData.Share" /> for the handle's own state as a
/// transfer attaches and detaches and as it is cleaned up, every shared cache kind as a transfer takes it, and
/// <see cref="CurlLockData.Cookie" /> around each read and write of the shared cookies. Every transfer of every engine
/// checks cookie domains against the one embedded public suffix list snapshot, which never changes, so it is shared
/// whatever the handle says; while <see cref="CurlLockData.Psl" /> is shared, a transfer takes it under its lock as it
/// takes the shared caches (ADR-0617). An HSTS entry one attached transfer learns switches another's <c>http</c>
/// URL to <c>https</c> while <see cref="CurlLockData.Hsts" /> is shared.
/// </summary>
/// <param name="timeProvider">The clock the share handle's caches run on.</param>
internal sealed class ManagedShareHandle(TimeProvider timeProvider) : IShareHandle
{
    private readonly Lock gate = new();

    private readonly HashSet<CurlLockData> sharedKinds = [];

    private ShareLockCallback? lockCallback;

    private ShareUnlockCallback? unlockCallback;

    private int attachedTransfers;

    private bool cleanedUp;

    /// <summary>Gets the cookies the attached transfers share under <see cref="CurlLockData.Cookie" />.</summary>
    internal CookieStore Cookies { get; } = new();

    /// <summary>Gets the resolved names the attached transfers share under <see cref="CurlLockData.Dns" />.</summary>
    internal DnsCache DnsCache { get; } = new();

    /// <summary>Gets the TLS sessions the attached transfers share under <see cref="CurlLockData.SslSession" />.</summary>
    internal TlsSessionCache TlsSessions { get; } = new(timeProvider);

    /// <summary>Gets the pooled connections the attached transfers share under <see cref="CurlLockData.Connect" />.</summary>
    internal ConnectionCache Connections { get; } = new(timeProvider);

    /// <summary>Gets the HSTS cache the attached transfers share under <see cref="CurlLockData.Hsts" />.</summary>
    internal HstsTransferPolicy Hsts { get; } = new(timeProvider);

    /// <summary>
    /// <c>CURLSHOPT_SHARE</c>: starts sharing <paramref name="data" />. Cookies, DNS, TLS sessions, connections, the
    /// public suffix list and HSTS can be shared; anything else is <see cref="CurlShareCode.BadOption" />, as in
    /// curl 8.22.0's <c>curl_share_setopt</c>.
    /// </summary>
    /// <param name="data">The kind of data to share.</param>
    /// <returns><see cref="CurlShareCode.Ok" />, or why not.</returns>
    public CurlShareCode Share(CurlLockData data) => SetOption(() => sharedKinds.Add(data), data);

    /// <summary><c>CURLSHOPT_UNSHARE</c>: stops sharing <paramref name="data" />, so attached transfers use their engine's again.</summary>
    /// <param name="data">The kind of data to stop sharing.</param>
    /// <returns><see cref="CurlShareCode.Ok" />, or why not.</returns>
    public CurlShareCode Unshare(CurlLockData data) => SetOption(() => sharedKinds.Remove(data), data);

    /// <summary><c>CURLSHOPT_LOCKFUNC</c>: the callback that takes a lock.</summary>
    /// <param name="callback">The callback, or <see langword="null" /> for none.</param>
    /// <returns><see cref="CurlShareCode.Ok" />, or why not.</returns>
    public CurlShareCode SetLockCallback(ShareLockCallback? callback) => SetOption(() => lockCallback = callback, data: null);

    /// <summary><c>CURLSHOPT_UNLOCKFUNC</c>: the callback that releases a lock.</summary>
    /// <param name="callback">The callback, or <see langword="null" /> for none.</param>
    /// <returns><see cref="CurlShareCode.Ok" />, or why not.</returns>
    public CurlShareCode SetUnlockCallback(ShareUnlockCallback? callback) => SetOption(() => unlockCallback = callback, data: null);

    /// <summary>
    /// <c>curl_share_cleanup</c>: under the <see cref="CurlLockData.Share" /> lock, refuses with
    /// <see cref="CurlShareCode.InUse" /> while a transfer is attached, else starts closing the shared connections and frees
    /// the handle, so every later call answers <see cref="CurlShareCode.Invalid" />.
    /// </summary>
    /// <returns><see cref="CurlShareCode.Ok" />, or why not.</returns>
    public CurlShareCode Cleanup()
    {
        CurlShareCode code;
        lock (gate)
        {
            code = cleanedUp ? CurlShareCode.Invalid : UnderShareLock(null, () => attachedTransfers > 0 ? CurlShareCode.InUse : CurlShareCode.Ok);
            cleanedUp |= code == CurlShareCode.Ok;
        }

        if (code == CurlShareCode.Ok)
        {
            _ = Connections.DisposeAsync().AsTask();
        }

        return code;
    }

    /// <summary>
    /// Attaches <paramref name="transfer" /> for one perform: counts it under the <see cref="CurlLockData.Share" /> lock,
    /// so option changes and <see cref="Cleanup" /> answer <see cref="CurlShareCode.InUse" /> until <see cref="Detach" />,
    /// and gives <paramref name="engine" />'s state with each cache this handle shares in place of the engine's, taking
    /// each one under its kind's lock, the public suffix list first when it is shared. A handle already cleaned up
    /// shares nothing.
    /// </summary>
    /// <param name="transfer">The transfer about to perform.</param>
    /// <param name="engine">The transfer's engine's state.</param>
    /// <returns>The state the perform runs over.</returns>
    internal ManagedEngineSharedState Attach(IEasyTransfer transfer, ManagedEngineSharedState engine)
    {
        lock (gate)
        {
            if (cleanedUp)
            {
                return engine;
            }

            UnderShareLock(transfer, () => ++attachedTransfers);
            _ = TakeShared(transfer, CurlLockData.Psl, own: true, engine: false);
            return new ManagedEngineSharedState(
                engine,
                TakeShared(transfer, CurlLockData.Connect, Connections, engine.Connections),
                TakeShared(transfer, CurlLockData.SslSession, TlsSessions, engine.TlsSessions),
                TakeShared(transfer, CurlLockData.Dns, DnsCache, engine.DnsCache),
                TakeShared(transfer, CurlLockData.Cookie, Cookies, engine.Cookies),
                TakeShared(transfer, CurlLockData.Hsts, Hsts, engine.Hsts));
        }
    }

    /// <summary>Detaches a transfer <see cref="Attach" /> attached, under the <see cref="CurlLockData.Share" /> lock.</summary>
    /// <param name="transfer">The transfer that performed.</param>
    internal void Detach(IEasyTransfer transfer)
    {
        lock (gate)
        {
            UnderShareLock(transfer, () => attachedTransfers = Math.Max(0, attachedTransfers - 1));
        }
    }

    /// <summary>
    /// The cookie store a transfer's HTTP handler keeps: <paramref name="store" /> with each call made under the
    /// <see cref="CurlLockData.Cookie" /> lock while this handle shares cookies, else <paramref name="store" /> itself.
    /// </summary>
    /// <param name="store">The transfer's cookie store, or <see langword="null" /> for none.</param>
    /// <param name="transfer">The transfer the lock callbacks are told of.</param>
    /// <returns>The store to keep.</returns>
    internal ICookieStore? CookieStoreFor(ICookieStore? store, IEasyTransfer transfer) =>
        store is not null && Shares(CurlLockData.Cookie) ? new LockedCookieStore(store, this, transfer) : store;

    /// <summary>Calls <paramref name="access" /> between the lock and unlock callbacks for <paramref name="data" />, taken for <see cref="CurlLockAccess.Single" /> use.</summary>
    /// <typeparam name="T">What the access answers.</typeparam>
    /// <param name="transfer">The transfer asking, or <see langword="null" /> for the share handle itself.</param>
    /// <param name="data">The kind of data accessed.</param>
    /// <param name="access">The access.</param>
    /// <returns>What <paramref name="access" /> answered.</returns>
    internal T Locked<T>(IEasyTransfer? transfer, CurlLockData data, Func<T> access)
    {
        lockCallback?.Invoke(transfer, data, CurlLockAccess.Single);
        try
        {
            return access();
        }
        finally
        {
            unlockCallback?.Invoke(transfer, data);
        }
    }

    /// <summary>Tells whether <paramref name="data" /> is shared now.</summary>
    /// <param name="data">The kind of data.</param>
    /// <returns><see langword="true" /> when it is.</returns>
    private bool Shares(CurlLockData data)
    {
        lock (gate)
        {
            return sharedKinds.Contains(data);
        }
    }

    /// <summary>
    /// One <c>curl_share_setopt</c>: <see cref="CurlShareCode.Invalid" /> once cleaned up,
    /// <see cref="CurlShareCode.InUse" /> while a transfer is attached, <see cref="CurlShareCode.BadOption" /> for a
    /// <paramref name="data" /> that cannot be shared, else <paramref name="set" /> and <see cref="CurlShareCode.Ok" />.
    /// </summary>
    /// <param name="set">Changes the option.</param>
    /// <param name="data">The kind of data a share or unshare names, or <see langword="null" /> for a callback.</param>
    /// <returns>The call's result.</returns>
    private CurlShareCode SetOption(Action set, CurlLockData? data)
    {
        lock (gate)
        {
            CurlShareCode code = cleanedUp ? CurlShareCode.Invalid
                : attachedTransfers > 0 ? CurlShareCode.InUse
                : data is null || IsShareable(data.Value) ? CurlShareCode.Ok
                : CurlShareCode.BadOption;
            if (code == CurlShareCode.Ok)
            {
                set();
            }

            return code;
        }
    }

    /// <summary>Tells whether <paramref name="data" /> is a kind a share handle shares (<c>curl_share_setopt</c>'s cases).</summary>
    /// <param name="data">The kind of data.</param>
    /// <returns><see langword="true" /> for cookies, DNS, TLS sessions, connections, the public suffix list and HSTS.</returns>
    private static bool IsShareable(CurlLockData data) => data is >= CurlLockData.Cookie and <= CurlLockData.Hsts;

    /// <summary>Calls <paramref name="access" /> under the <see cref="CurlLockData.Share" /> lock.</summary>
    /// <typeparam name="T">What the access answers.</typeparam>
    /// <param name="transfer">The transfer asking, or <see langword="null" /> for the share handle itself.</param>
    /// <param name="access">The access to the handle's own state.</param>
    /// <returns>What <paramref name="access" /> answered.</returns>
    private T UnderShareLock<T>(IEasyTransfer? transfer, Func<T> access) => Locked(transfer, CurlLockData.Share, access);

    /// <summary>The share handle's <paramref name="own" /> cache, taken under its kind's lock, while it shares <paramref name="data" />; else the engine's.</summary>
    /// <typeparam name="T">The cache's type.</typeparam>
    /// <param name="transfer">The transfer taking it.</param>
    /// <param name="data">The kind of data the cache holds.</param>
    /// <param name="own">The share handle's cache.</param>
    /// <param name="engine">The engine's cache.</param>
    /// <returns>The cache the transfer uses.</returns>
    private T TakeShared<T>(IEasyTransfer transfer, CurlLockData data, T own, T engine) =>
        sharedKinds.Contains(data) ? Locked(transfer, data, () => own) : engine;

    /// <summary>A transfer's cookie store with each call made under the share handle's <see cref="CurlLockData.Cookie" /> lock.</summary>
    /// <param name="inner">The transfer's cookie store over the shared cookies.</param>
    /// <param name="share">The share handle whose callbacks lock.</param>
    /// <param name="transfer">The transfer the callbacks are told of.</param>
    private sealed class LockedCookieStore(ICookieStore inner, ManagedShareHandle share, IEasyTransfer transfer) : ICookieStore
    {
        public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, ITransferEvents events) =>
            share.Locked(transfer, CurlLockData.Cookie, () => inner.GetCookieHeader(url, secure, now, events));

        public string? GetStoredCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, ITransferEvents events) =>
            share.Locked(transfer, CurlLockData.Cookie, () => inner.GetStoredCookieHeader(url, secure, now, events));

        public int StoreFromResponse(CurlUrl url, string setCookieHeader, int storedFromResponse, DateTimeOffset now, ITransferEvents events) =>
            share.Locked(transfer, CurlLockData.Cookie, () => inner.StoreFromResponse(url, setCookieHeader, storedFromResponse, now, events));
    }
}
