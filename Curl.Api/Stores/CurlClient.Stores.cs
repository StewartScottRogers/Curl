using Curl.Api.EngineContracts;

namespace Curl.Api;

public sealed partial class CurlClient
{
    private CurlStores? _ownStores;

    /// <summary>
    /// The client's cookie, HSTS and alt-svc stores: <see cref="CurlClientOptions.Stores"/> when set,
    /// otherwise the client's own, made over its engine on first use. As in libcurl, the cookie
    /// engine and HSTS are off until then: only transfers started after the stores exist use them.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    public CurlStores Stores => Options.Stores ?? OwnStores();

    /// <summary>The cookies of <see cref="Stores"/>.</summary>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    public CurlCookieStore Cookies => Stores.Cookies;

    /// <summary>The HSTS entries of <see cref="Stores"/>.</summary>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    public CurlHstsStore Hsts => Stores.Hsts;

    /// <summary>The alt-svc cache of <see cref="Stores"/>.</summary>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    public CurlAltSvcStore AltSvc => Stores.AltSvc;

    /// <summary>Binds <paramref name="easy"/> to the client's stores, or returns it as it is while the client has none.</summary>
    /// <param name="easy">A new handle.</param>
    /// <returns>The handle the client hands out.</returns>
    private IEasyTransfer BindStores(IEasyTransfer easy) =>
        (Options.Stores ?? Volatile.Read(ref _ownStores)) is { } stores ? stores.Bind(easy) : easy;

    /// <summary>Disposes the client's own stores, if it made any.</summary>
    private void DisposeOwnStores() => Interlocked.Exchange(ref _ownStores, null)?.Dispose();

    private CurlStores OwnStores()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Volatile.Read(ref _ownStores) is { } existing)
        {
            return existing;
        }

        CurlStores created = new(_engine);
        CurlStores? raced = Interlocked.CompareExchange(ref _ownStores, created, null);
        if (raced is null)
        {
            return created;
        }

        created.Dispose();
        return raced;
    }
}
