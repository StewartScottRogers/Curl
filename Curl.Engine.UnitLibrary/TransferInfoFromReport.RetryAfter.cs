using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class TransferInfoFromReport
{
    /// <summary>
    /// The last response's <c>Retry-After</c>, read as libcurl's <c>http_header_r</c> reads it whatever the
    /// response code (<see cref="RetryAfterHeader" />); zero when it has none.
    /// </summary>
    private static TimeSpan RetryAfterOf(TransferReport report, TimeProvider timeProvider) =>
        TimeSpan.FromSeconds(report.ResponseHeaders
            .Where(header => string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
            .Select(header => RetryAfterHeader.ParseSeconds(header.Value, timeProvider.GetUtcNow()))
            .LastOrDefault());
}
