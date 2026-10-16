using Curl.Api.EngineContracts;
using Curl.Api.Errors;
using Curl.Api.Info;

namespace Curl.Api;

public sealed partial class CurlClient
{
    /// <summary>The <see cref="Version"/> of each <c>CURL_HTTP_VERSION_*</c> value a transfer reports.</summary>
    private static readonly Dictionary<long, Version> HttpVersions = new()
    {
        [1] = System.Net.HttpVersion.Version10,
        [2] = System.Net.HttpVersion.Version11,
        [3] = System.Net.HttpVersion.Version20,
        [30] = System.Net.HttpVersion.Version30,
    };

    /// <summary>
    /// Starts <paramref name="request"/> on a new easy handle with <see cref="Options"/> applied and
    /// returns the final response as soon as its body starts or the transfer ends (ADR-0635). The
    /// body streams from <see cref="CurlResponse.Body"/> while the transfer goes on; dispose it to
    /// free the handle, or before the end to abort the transfer.
    /// </summary>
    /// <param name="request">The request to perform.</param>
    /// <param name="cancellationToken">Stops the transfer, while the body streams too; it then ends with the engine's error code.</param>
    /// <returns>The final response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    /// <exception cref="CurlException">The transfer failed before its body started; its <see cref="CurlException.Code"/> says how.</exception>
    public Task<CurlResponse> SendAsync(CurlRequest request, CancellationToken cancellationToken) =>
        SendWithBodyCapacityAsync(request, ResponseBodyStream.DefaultCapacity, cancellationToken);

    /// <summary><see cref="SendAsync(CurlRequest, CancellationToken)"/> with the body buffer's capacity chosen.</summary>
    /// <param name="request">The request to perform.</param>
    /// <param name="bodyCapacity">The body bytes buffered before the transfer is paused for the reader.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>The final response.</returns>
    internal async Task<CurlResponse> SendWithBodyCapacityAsync(CurlRequest request, int bodyCapacity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IEasyTransfer easy = CreateEasy();
        ResponseHeaderCollector collector = new();
        ResponseBodyStream body = new(easy, bodyCapacity, cancellationToken);
        easy.Options = CurlTransportOptions.Apply(easy.Options, request.Network, request.Tls, request.Proxy);
        easy.Options = CurlRequestOptions.Apply(easy.Options, request, collector.Take, body.TakeBody);
        await body.StartAsync().ConfigureAwait(false);
        TransferInfo info = easy.Info;
        return new CurlResponse
        {
            StatusCode = collector.StatusCode != 0 ? collector.StatusCode : (int)info.ResponseCode,
            ReasonPhrase = collector.ReasonPhrase,
            Version = VersionOf(collector.HttpVersion != 0 ? collector.HttpVersion : info.HttpVersion),
            Headers = collector.Headers,
            Trailers = collector.Trailers,
            Info = info,
            Body = body,
        };
    }

    /// <summary>The <see cref="Version"/> of a <c>CURLINFO_HTTP_VERSION</c> value.</summary>
    /// <param name="httpVersion">A <c>CURL_HTTP_VERSION_*</c> value.</param>
    /// <returns>1.0, 1.1, 2.0 or 3.0; <see cref="System.Net.HttpVersion.Unknown"/> for any other value.</returns>
    internal static Version VersionOf(long httpVersion) =>
        HttpVersions.GetValueOrDefault(httpVersion, System.Net.HttpVersion.Unknown);
}
