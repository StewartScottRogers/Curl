using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Errors;
using Curl.Api.Options;

namespace Curl.Api;

/// <summary>
/// The cookies, HSTS entries and alt-svc cache one or more <see cref="CurlClient"/>s keep across
/// their transfers (ADR-0645). It owns one <see cref="IShareHandle"/> of its engine sharing
/// <see cref="CurlLockData.Cookie"/> and <see cref="CurlLockData.Hsts"/>, and every transfer of a
/// client using it points <c>CURLOPT_SHARE</c> at that handle, so transfers running at the same time
/// see each other's cookies and HSTS entries. Two clients share the stores by both naming them in
/// <see cref="CurlClientOptions.Stores"/>; they must run on the engine that made them. Disposing the
/// stores cleans the share handle up.
/// </summary>
public sealed class CurlStores : IDisposable
{
    private readonly IShareHandle _share;
    private int _disposed;

    /// <summary>Creates the stores with a new share handle of <paramref name="engine"/> sharing cookies and HSTS.</summary>
    /// <param name="engine">The engine whose transfers use the stores.</param>
    /// <exception cref="CurlException">The share handle refused to share cookies or HSTS (<see cref="CurlCode.FailedInit"/>); it is cleaned up.</exception>
    public CurlStores(ICurlEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _share = engine.CreateShare();
        CurlShareCode code = _share.Share(CurlLockData.Cookie);
        code = code == CurlShareCode.Ok ? _share.Share(CurlLockData.Hsts) : code;
        if (code != CurlShareCode.Ok)
        {
            _share.Cleanup();
            throw new CurlException(CurlCode.FailedInit, $"curl_share_setopt failed: {code}");
        }
    }

    /// <summary>The cookie store.</summary>
    public CurlCookieStore Cookies { get; } = new();

    /// <summary>The HSTS store.</summary>
    public CurlHstsStore Hsts { get; } = new();

    /// <summary>The alt-svc cache.</summary>
    public CurlAltSvcStore AltSvc { get; } = new();

    /// <summary><c>curl_share_cleanup</c> on the share handle. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _share.Cleanup();
        }
    }

    /// <summary>Wraps <paramref name="easy"/> so its transfers start from the stores and leave their state in them (<see cref="StoreBoundEasyTransfer"/>).</summary>
    /// <param name="easy">The handle.</param>
    /// <returns>The wrapped handle, which owns <paramref name="easy"/>.</returns>
    internal IEasyTransfer Bind(IEasyTransfer easy) => new StoreBoundEasyTransfer(easy, this);

    /// <summary>
    /// The options a transfer starts with: <c>CURLOPT_SHARE</c> at the share handle, the cookie
    /// engine on with the store's cookies, HSTS on with the store's callbacks, and the alt-svc cache
    /// file once one is in use.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The options with the stores applied.</returns>
    /// <exception cref="ObjectDisposedException">The stores have been disposed.</exception>
    internal EasyOptions ApplyTo(EasyOptions options)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return options with
        {
            Behaviour = options.Behaviour with { Share = _share },
            Http = AltSvc.ApplyTo(CurlHstsStore.ApplyTo(Cookies.ApplyTo(options.Http))),
            Callbacks = Hsts.ApplyTo(options.Callbacks),
        };
    }
}
