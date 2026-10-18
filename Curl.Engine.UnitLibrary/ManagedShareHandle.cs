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
/// transfer attaches through <c>CURLOPT_SHARE</c> and detaches (<see cref="Reattach" />) and as it is cleaned up, every
/// shared cache kind as a performing transfer takes it, and <see cref="CurlLockData.Cookie" /> around each read and
/// write of the shared cookies, each <c>CURLOPT_COOKIELIST</c> item and each attached transfer's cleanup (<see cref="LockCookies" />). Every transfer of every engine
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

    /// <summary>The <c>host:port</c> names attached performs have resolved while DNS was shared, as libcurl's shared DNS cache holds them.</summary>
    private readonly HashSet<string> resolvedHosts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The <c>host:port</c> names attached performs have opened a connection for while connections were shared, as libcurl's shared connection pool holds them.</summary>
    private readonly HashSet<string> connectedHosts = new(StringComparer.OrdinalIgnoreCase);

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
    /// <c>curl_share_cleanup</c>: refuses with <see cref="CurlShareCode.InUse" /> while a transfer is attached, without
    /// calling the lock callbacks (upstream test506), else, under the <see cref="CurlLockData.Share" /> lock, starts
    /// closing the shared connections and frees the handle, so every later call answers <see cref="CurlShareCode.Invalid" />.
    /// </summary>
    /// <returns><see cref="CurlShareCode.Ok" />, or why not.</returns>
    public CurlShareCode Cleanup()
    {
        CurlShareCode code;
        lock (gate)
        {
            code = cleanedUp ? CurlShareCode.Invalid
                : attachedTransfers > 0 ? CurlShareCode.InUse
                : LockForCleanup();
            cleanedUp |= code == CurlShareCode.Ok;
        }

        if (code == CurlShareCode.Ok)
        {
            _ = Connections.DisposeAsync().AsTask();
        }

        return code;
    }

    /// <summary>
    /// <c>CURLOPT_SHARE</c> naming a share handle where <paramref name="current" /> was named before, as curl 8.22.0's
    /// <c>lib/setopt.c</c>: detaches <paramref name="transfer" /> from <paramref name="current" /> and attaches it to
    /// <paramref name="named" />, each under that handle's <see cref="CurlLockData.Share" /> lock, so option changes and
    /// <see cref="Cleanup" /> answer <see cref="CurlShareCode.InUse" /> while it stays attached. Naming the same handle
    /// again changes nothing, and a handle already cleaned up is not attached.
    /// </summary>
    /// <param name="current">The share handle the transfer is attached to, or <see langword="null" /> for none.</param>
    /// <param name="named">The share handle the option names now, or <see langword="null" /> for none.</param>
    /// <param name="transfer">The transfer whose option changed.</param>
    /// <returns>The share handle the transfer is attached to now, or <see langword="null" /> for none.</returns>
    internal static ManagedShareHandle? Reattach(ManagedShareHandle? current, ManagedShareHandle? named, IEasyTransfer transfer)
    {
        if (ReferenceEquals(current, named))
        {
            return current;
        }

        current?.ChangeAttachedCount(transfer, -1);
        return named is not null && named.ChangeAttachedCount(transfer, 1) ? named : null;
    }

    /// <summary>
    /// Calls the <see cref="CurlLockData.Cookie" /> lock and unlock callbacks <paramref name="times" /> times while this
    /// handle shares cookies, as curl 8.22.0 takes the lock for each <c>CURLOPT_COOKIELIST</c> item as it is set and for
    /// the cookie flush of <c>curl_easy_cleanup</c> (upstream test506).
    /// </summary>
    /// <param name="transfer">The transfer the callbacks are told of.</param>
    /// <param name="times">How many times to lock; none when it is 0 or less.</param>
    internal void LockCookies(IEasyTransfer transfer, int times)
    {
        for (int time = 0; time < times && Shares(CurlLockData.Cookie); time++)
        {
            _ = Locked(transfer, CurlLockData.Cookie, () => time);
        }
    }

    /// <summary>Gets a value indicating whether this handle shares cookies now (<see cref="CurlLockData.Cookie" />).</summary>
    internal bool SharesCookies => Shares(CurlLockData.Cookie);

    /// <summary>
    /// Applies each <c>CURLOPT_COOKIELIST</c> item to the shared cookies under its own <see cref="CurlLockData.Cookie" />
    /// lock and unlock callbacks, as curl 8.22.0's <c>lib/setopt.c</c> applies an item as it is set (upstream test506).
    /// </summary>
    /// <param name="transfer">The transfer the callbacks are told of.</param>
    /// <param name="items">The items, in the order set.</param>
    /// <param name="apply">Applies one item to the shared cookies.</param>
    internal void ApplyCookieListItems(IEasyTransfer transfer, IEnumerable<string> items, Action<CookieStore, string> apply)
    {
        foreach (string item in items)
        {
            _ = Locked(transfer, CurlLockData.Cookie, () =>
            {
                apply(Cookies, item);
                return item;
            });
        }
    }

    /// <summary>
    /// <c>CURLINFO_COOKIELIST</c> over the shared cookies: every cookie as one Netscape cookie-file line, in the order
    /// stored as <see cref="CookieEngine.ListLines" /> lists them, read under one <see cref="CurlLockData.Cookie" /> lock and unlock pair, as curl 8.22.0's
    /// <c>lib/getinfo.c</c> takes it (upstream test506).
    /// </summary>
    /// <param name="transfer">The transfer the callbacks are told of.</param>
    /// <returns>The lines.</returns>
    internal IReadOnlyList<string> ListCookies(IEasyTransfer transfer) =>
        Locked(transfer, CurlLockData.Cookie, () => (IReadOnlyList<string>)[.. Cookies.Cookies.Select(NetscapeCookieFile.FormatLine)]);

    /// <summary>
    /// Gives <paramref name="engine" />'s state for one perform of an attached transfer, with each cache this handle
    /// shares in place of the engine's, taking each one under its kind's lock, the public suffix list first when it is
    /// shared. The cookies and DNS cache are taken as curl 8.22.0's perform takes them (upstream test506): the cookie
    /// lock once for loading <c>CURLOPT_COOKIEFILE</c>'s files when <paramref name="loadsCookieFiles" /> and once more,
    /// then the DNS lock once to prune the cache, once to look <paramref name="hostKey" /> up and, the first time the
    /// handle sees it, once more to add it (<see cref="TakeDns" />). A handle already cleaned up shares nothing.
    /// </summary>
    /// <param name="transfer">The transfer about to perform.</param>
    /// <param name="engine">The transfer's engine's state.</param>
    /// <param name="loadsCookieFiles">Whether the transfer loads cookie files before it connects.</param>
    /// <param name="hostKey">The <c>host:port</c> the transfer resolves, or <see langword="null" /> when its URL does not parse.</param>
    /// <returns>The state the perform runs over.</returns>
    internal ManagedEngineSharedState Attach(IEasyTransfer transfer, ManagedEngineSharedState engine, bool loadsCookieFiles, string? hostKey)
    {
        lock (gate)
        {
            if (cleanedUp)
            {
                return engine;
            }

            _ = TakeShared(transfer, CurlLockData.Psl, own: true, engine: false);
            ConnectionCache connections = TakeConnections(transfer, hostKey, engine.Connections);
            TlsSessionCache tlsSessions = TakeShared(transfer, CurlLockData.SslSession, TlsSessions, engine.TlsSessions);
            CookieStore cookies = TakeCookies(transfer, loadsCookieFiles, engine.Cookies);
            return new ManagedEngineSharedState(
                engine,
                connections,
                tlsSessions,
                TakeDns(transfer, hostKey, engine.DnsCache),
                cookies,
                TakeShared(transfer, CurlLockData.Hsts, Hsts, engine.Hsts));
        }
    }

    /// <summary>
    /// Calls the <see cref="CurlLockData.Dns" /> lock and unlock callbacks once while this handle shares DNS, as curl
    /// 8.22.0 unlinks the connection's DNS entry once a perform is done (upstream test506).
    /// </summary>
    /// <param name="transfer">The transfer whose perform is done.</param>
    internal void LockDnsAtDone(IEasyTransfer transfer)
    {
        if (Shares(CurlLockData.Dns))
        {
            _ = Locked(transfer, CurlLockData.Dns, () => true);
        }
    }

    /// <summary>
    /// The locks <see cref="Cleanup" /> takes, as curl 8.22.0's <c>curl_share_cleanup</c> does: <see cref="CurlLockData.Share" />,
    /// then <see cref="CurlLockData.Connect" /> while connections are shared, as the pool is destroyed (upstream test1554).
    /// </summary>
    /// <returns><see cref="CurlShareCode.Ok" />.</returns>
    private CurlShareCode LockForCleanup()
    {
        CurlShareCode code = UnderShareLock(null, () => CurlShareCode.Ok);
        return sharedKinds.Contains(CurlLockData.Connect) ? Locked(null, CurlLockData.Connect, () => code) : code;
    }

    /// <summary>
    /// Calls the <see cref="CurlLockData.Connect" /> lock and unlock callbacks once while this handle shares
    /// connections, as curl 8.22.0 returns the connection to the shared pool once a perform is done (upstream test1554).
    /// </summary>
    /// <param name="transfer">The transfer whose perform is done.</param>
    internal void LockConnectionsAtDone(IEasyTransfer transfer)
    {
        if (Shares(CurlLockData.Connect))
        {
            _ = Locked(transfer, CurlLockData.Connect, () => true);
        }
    }

    /// <summary>
    /// The connection cache a perform uses: this handle's, after the connect lock three times while it shares
    /// connections (the pool checks its limits, looks for a connection to reuse and counts it), and a fourth when
    /// <paramref name="hostKey" /> is one this handle has not connected to yet, which adds the new connection (upstream
    /// test1554); else <paramref name="engine" />.
    /// </summary>
    /// <param name="transfer">The transfer taking it.</param>
    /// <param name="hostKey">The <c>host:port</c> the transfer connects to, or <see langword="null" />.</param>
    /// <param name="engine">The engine's connection cache.</param>
    /// <returns>The connection cache the transfer uses.</returns>
    private ConnectionCache TakeConnections(IEasyTransfer transfer, string? hostKey, ConnectionCache engine)
    {
        if (!sharedKinds.Contains(CurlLockData.Connect))
        {
            return engine;
        }

        int locks = hostKey is not null && connectedHosts.Add(hostKey) ? 4 : 3;
        for (int time = 0; time < locks; time++)
        {
            _ = Locked(transfer, CurlLockData.Connect, () => time);
        }

        return Connections;
    }

    /// <summary>
    /// The cookie store a perform uses: this handle's, after the cookie lock once for loading the cookie files when
    /// <paramref name="loadsCookieFiles" /> and once more, while it shares cookies; else <paramref name="engine" />.
    /// </summary>
    /// <param name="transfer">The transfer taking it.</param>
    /// <param name="loadsCookieFiles">Whether the transfer loads cookie files.</param>
    /// <param name="engine">The engine's cookie store.</param>
    /// <returns>The cookie store the transfer uses.</returns>
    private CookieStore TakeCookies(IEasyTransfer transfer, bool loadsCookieFiles, CookieStore engine)
    {
        LockCookies(transfer, loadsCookieFiles ? 1 : 0);
        return TakeShared(transfer, CurlLockData.Cookie, Cookies, engine);
    }

    /// <summary>
    /// The DNS cache a perform uses: this handle's, after the DNS lock to prune it and, for a URL that parsed, to look
    /// <paramref name="hostKey" /> up and to add it the first time this handle sees it, while it shares DNS; else
    /// <paramref name="engine" />.
    /// </summary>
    /// <param name="transfer">The transfer taking it.</param>
    /// <param name="hostKey">The <c>host:port</c> the transfer resolves, or <see langword="null" />.</param>
    /// <param name="engine">The engine's DNS cache.</param>
    /// <returns>The DNS cache the transfer uses.</returns>
    private DnsCache TakeDns(IEasyTransfer transfer, string? hostKey, DnsCache engine)
    {
        if (!sharedKinds.Contains(CurlLockData.Dns))
        {
            return engine;
        }

        int locks = hostKey is null ? 1 : resolvedHosts.Add(hostKey) ? 3 : 2;
        for (int time = 0; time < locks; time++)
        {
            _ = Locked(transfer, CurlLockData.Dns, () => time);
        }

        return DnsCache;
    }

    /// <summary>
    /// Counts a transfer attaching (<paramref name="change" /> 1) or detaching (-1) under the
    /// <see cref="CurlLockData.Share" /> lock; a handle already cleaned up attaches none.
    /// </summary>
    /// <param name="transfer">The transfer attaching or detaching.</param>
    /// <param name="change">1 to attach, -1 to detach.</param>
    /// <returns><see langword="true" /> when the count changed.</returns>
    private bool ChangeAttachedCount(IEasyTransfer transfer, int change)
    {
        lock (gate)
        {
            if (cleanedUp)
            {
                return false;
            }

            UnderShareLock(transfer, () => attachedTransfers += change);
            return true;
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
