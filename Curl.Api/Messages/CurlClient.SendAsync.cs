using System.Runtime.ExceptionServices;
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
    /// free the handle, or before the end to abort the transfer. The handle and every handle the
    /// request body created (a multipart body's MIME builder) are disposed when the transfer ends.
    /// </summary>
    /// <param name="request">The request to perform.</param>
    /// <param name="cancellationToken">
    /// Aborts the transfer, whether it is connecting, uploading, downloading or paused, while the body
    /// streams too (ADR-0639); the call or the body's next read then throws <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>The final response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled before the body started; no handle is left open.</exception>
    /// <exception cref="CurlException">The transfer failed before its body started, a timeout included; its <see cref="CurlException.Code"/> says how.</exception>
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
        cancellationToken.ThrowIfCancellationRequested();
        IEasyTransfer easy = CreateEasy();
        ResponseHeaderCollector collector = new();
        ResponseBodyStream body = new(easy, bodyCapacity, cancellationToken, request.Progress);
        ExceptionDispatchInfo? setUpFailure = TrySetUp(easy, request, collector, body);
        if (setUpFailure is not null)
        {
            await body.DisposeUnstartedAsync().ConfigureAwait(false);
        }

        setUpFailure?.Throw();

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

    /// <summary>
    /// Puts <paramref name="request"/>'s transport options, method, URL, headers, body and callbacks on
    /// <paramref name="easy"/>, catching a failure (a multipart body's failing setter) so the caller can
    /// dispose the handles before rethrowing it, without awaiting inside a <see langword="catch"/>.
    /// </summary>
    /// <param name="easy">The handle.</param>
    /// <param name="request">The request.</param>
    /// <param name="collector">The header callback's collector.</param>
    /// <param name="body">The response body, whose write callback and owned handles the request takes.</param>
    /// <returns><see langword="null"/> when every option was set; otherwise the failure, to rethrow.</returns>
    private ExceptionDispatchInfo? TrySetUp(IEasyTransfer easy, CurlRequest request, ResponseHeaderCollector collector, ResponseBodyStream body)
    {
        try
        {
            easy.Options = CurlTransportOptions.Apply(easy.Options, request.Network, request.Tls, request.Proxy);
            easy.Options = CurlProtocolOptions.Apply(easy.Options, request.ProtocolRecords);
            easy.Options = CurlRequestOptions.Apply(easy.Options, request, collector.Take, body.TakeBody, _engine, body.Owned);
            return null;
        }
        catch (Exception exception)
        {
            return ExceptionDispatchInfo.Capture(exception);
        }
    }

    /// <summary>The <see cref="Version"/> of a <c>CURLINFO_HTTP_VERSION</c> value.</summary>
    /// <param name="httpVersion">A <c>CURL_HTTP_VERSION_*</c> value.</param>
    /// <returns>1.0, 1.1, 2.0 or 3.0; <see cref="System.Net.HttpVersion.Unknown"/> for any other value.</returns>
    internal static Version VersionOf(long httpVersion) =>
        HttpVersions.GetValueOrDefault(httpVersion, System.Net.HttpVersion.Unknown);
}
