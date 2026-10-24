using Curl.Api.Info;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>
    /// The info with the proxy error (<c>CURLINFO_PROXY_ERROR</c>) the report holds.
    /// </summary>
    /// <param name="info">The info so far.</param>
    /// <param name="report">The finished transfer's report.</param>
    /// <returns>The info with the proxy error set.</returns>
    private static TransferInfo WithProxyError(TransferInfo info, TransferReport report) => info with
    {
        ProxyError = (long)report.ProxyError,
    };
}
