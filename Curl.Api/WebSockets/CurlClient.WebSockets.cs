using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Errors;
using Curl.Api.Options;
using Curl.Api.Options.Network;

namespace Curl.Api;

/// <summary>The WebSocket half of <see cref="CurlClient"/> (BL-2508).</summary>
public sealed partial class CurlClient
{
    /// <summary>Opens a WebSocket to <paramref name="url"/> with libcurl's default WebSocket options.</summary>
    /// <param name="url">A <c>ws://</c> or <c>wss://</c> URL.</param>
    /// <param name="cancellationToken">Stops the connect and upgrade.</param>
    /// <returns>The open WebSocket; the caller disposes it.</returns>
    /// <exception cref="CurlException">The connect or upgrade failed.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    public Task<CurlWebSocket> ConnectWebSocketAsync(string url, CancellationToken cancellationToken = default) =>
        ConnectWebSocketAsync(url, new CurlWebSocketOptions(), cancellationToken);

    /// <summary>
    /// Opens a WebSocket to <paramref name="url"/>: a new easy handle with <see cref="Options"/>,
    /// <c>CURLOPT_CONNECT_ONLY</c> 2 and <paramref name="options"/>' <c>CURLOPT_WS_OPTIONS</c>,
    /// performed until the connection is upgraded.
    /// </summary>
    /// <param name="url">A <c>ws://</c> or <c>wss://</c> URL.</param>
    /// <param name="options">The WebSocket options.</param>
    /// <param name="cancellationToken">Stops the connect and upgrade.</param>
    /// <returns>The open WebSocket; the caller disposes it.</returns>
    /// <exception cref="CurlException">The connect or upgrade failed; the handle is disposed.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    public Task<CurlWebSocket> ConnectWebSocketAsync(string url, CurlWebSocketOptions options, CancellationToken cancellationToken = default) =>
        ConnectWebSocketAsync(url, options, CurlWebSocket.WaitRetryInterval, cancellationToken);

    /// <summary>
    /// <see cref="ConnectWebSocketAsync(string, CurlWebSocketOptions, CancellationToken)"/> with the
    /// wait the WebSocket makes before asking again after <see cref="CurlCode.Again"/>.
    /// </summary>
    /// <param name="url">A <c>ws://</c> or <c>wss://</c> URL.</param>
    /// <param name="options">The WebSocket options.</param>
    /// <param name="waitBeforeRetry">The WebSocket's wait before a retry.</param>
    /// <param name="cancellationToken">Stops the connect and upgrade.</param>
    /// <returns>The open WebSocket.</returns>
    internal async Task<CurlWebSocket> ConnectWebSocketAsync(string url, CurlWebSocketOptions options, Func<CancellationToken, Task> waitBeforeRetry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(options);
        IEasyTransfer easy = CreateEasy();
        EasyOptions current = easy.Options;
        easy.Options = current with
        {
            Network = current.Network with { Url = url, ConnectOnly = ConnectOnly.WebSocket },
            Protocols = current.Protocols with { WebSocket = current.Protocols.WebSocket with { Options = options.Bits } },
        };
        Task<CurlCode> perform = easy.PerformAsync(cancellationToken).AsTask();
        await ((Task)perform).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (perform.IsCompletedSuccessfully && await perform.ConfigureAwait(false) == CurlCode.Ok)
        {
            return new CurlWebSocket(easy, waitBeforeRetry);
        }

        string errorText = perform.IsCompletedSuccessfully ? easy.ErrorText : string.Empty;
        await easy.DisposeAsync().ConfigureAwait(false);
        CurlCode failed = await perform.ConfigureAwait(false);
        throw new CurlException(failed, errorText.Length > 0 ? errorText : CurlCodeText.Of(failed));
    }
}
