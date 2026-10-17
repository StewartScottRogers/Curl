using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api;

/// <summary>
/// An easy handle bound to a <see cref="CurlStores"/>: <see cref="PerformAsync"/> first applies the
/// stores to the handle's options (<see cref="CurlStores.ApplyTo"/>), so they hold whatever the caller
/// set, and <see cref="DisposeAsync"/>, after a perform, leaves the transfer's
/// <c>CURLINFO_COOKIELIST</c> in the cookie store before <c>curl_easy_cleanup</c>, which hands the
/// HSTS cache to the store's write callback. Every other member is the wrapped handle's.
/// </summary>
/// <param name="inner">The wrapped handle, owned.</param>
/// <param name="stores">The stores.</param>
internal sealed class StoreBoundEasyTransfer(IEasyTransfer inner, CurlStores stores) : IEasyTransfer
{
    private bool _performed;

    /// <inheritdoc/>
    public EasyOptions Options
    {
        get => inner.Options;
        set => inner.Options = value;
    }

    /// <inheritdoc/>
    public TransferInfo Info => inner.Info;

    /// <inheritdoc/>
    public string ErrorText => inner.ErrorText;

    /// <inheritdoc/>
    public ITransferHeaders Headers => inner.Headers;

    /// <inheritdoc/>
    public IEasyWebSocket WebSocket => inner.WebSocket;

    /// <inheritdoc/>
    public IConnectOnlyConnection Connection => inner.Connection;

    /// <inheritdoc/>
    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        inner.Options = stores.ApplyTo(inner.Options);
        _performed = true;
        return inner.PerformAsync(cancellationToken);
    }

    /// <summary><c>curl_easy_duphandle</c>, the new handle bound to the same stores.</summary>
    /// <returns>The new handle; the caller disposes it.</returns>
    public IEasyTransfer Duplicate() => stores.Bind(inner.Duplicate());

    /// <inheritdoc/>
    public void Reset() => inner.Reset();

    /// <inheritdoc/>
    public CurlCode Pause(CurlPause directions) => inner.Pause(directions);

    /// <inheritdoc/>
    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => inner.UpkeepAsync(cancellationToken);

    /// <summary>Leaves the last perform's cookie list in the cookie store, then disposes the wrapped handle.</summary>
    /// <returns>A task that completes when the wrapped handle is disposed.</returns>
    public ValueTask DisposeAsync()
    {
        if (_performed)
        {
            stores.Cookies.ReplaceWith(inner.Info.CookieList);
        }

        return inner.DisposeAsync();
    }
}
