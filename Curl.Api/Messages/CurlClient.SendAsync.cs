using Curl.Api.Codes;
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
    /// Performs <paramref name="request"/> on a new easy handle with <see cref="Options"/> applied,
    /// disposes the handle, and returns the final response. The body stream is empty for now (BL-2501).
    /// </summary>
    /// <param name="request">The request to perform.</param>
    /// <param name="cancellationToken">Stops the transfer; it then ends with the engine's error code.</param>
    /// <returns>The final response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    /// <exception cref="CurlException">The transfer failed; its <see cref="CurlException.Code"/> says how.</exception>
    public async Task<CurlResponse> SendAsync(CurlRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IEasyTransfer easy = CreateEasy();
        await using (easy.ConfigureAwait(false))
        {
            ResponseHeaderCollector collector = new();
            easy.Options = CurlRequestOptions.Apply(easy.Options, request, collector.Take);
            CurlCode code = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            CurlException.ThrowIfFailed(code, easy.ErrorText);
            TransferInfo info = easy.Info;
            return new CurlResponse
            {
                StatusCode = (int)info.ResponseCode,
                ReasonPhrase = collector.ReasonPhrase,
                Version = VersionOf(info.HttpVersion),
                Headers = collector.Headers,
                Trailers = collector.Trailers,
                Info = info,
            };
        }
    }

    /// <summary>The <see cref="Version"/> of a <c>CURLINFO_HTTP_VERSION</c> value.</summary>
    /// <param name="httpVersion">A <c>CURL_HTTP_VERSION_*</c> value.</param>
    /// <returns>1.0, 1.1, 2.0 or 3.0; <see cref="System.Net.HttpVersion.Unknown"/> for any other value.</returns>
    internal static Version VersionOf(long httpVersion) =>
        HttpVersions.GetValueOrDefault(httpVersion, System.Net.HttpVersion.Unknown);
}
