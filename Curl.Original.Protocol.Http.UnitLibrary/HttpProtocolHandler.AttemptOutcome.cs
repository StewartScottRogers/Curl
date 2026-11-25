using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that defines HttpAttemptOutcome, what one attempt came to.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// How one exchange ended: its result, and whether a retry follows.
    /// </summary>
    /// <param name="Result">The result, with the report so far.</param>
    /// <param name="Retry">
    /// The request to send next, or <see langword="null" /> when the result is final.
    /// </param>
    /// <param name="KeepsAlive">
    /// <see langword="true" /> when the response was read to its end and leaves the connection
    /// open, so a retry may be sent on it and it is marked reusable.
    /// </param>
    private sealed class HttpAttemptOutcome(TransferResult Result, HttpRequestPlan? Retry, bool KeepsAlive)
    {
        /// <summary>
        /// Gets a value indicating whether the exchange ran on a pooled connection that died
        /// before its response began, so <see cref="Retry" /> resends the request on a fresh one.
        /// </summary>
        public bool DiedBeforeResponse { get; init; }

        /// <summary>
        /// Gets a value indicating whether the exchange is the sixth refused one, given up on, whose HTTP/2 connection curl closes without a GOAWAY.
        /// </summary>
        public bool GaveUpOnRefusedStream { get; init; }

        /// <summary>
        /// Gets a value indicating whether the exchange ran whole and only <c>--fail-with-body</c>
        /// or <c>--max-redirs</c> failed it, so its connection ends as a successful one's does.
        /// </summary>
        public bool FailedAfterBody { get; init; }

        /// <summary>
        /// Gets the <c>Maximum (N) redirects followed</c> line <c>-v</c> writes after the
        /// connection's end when <c>--max-redirs</c> refused the resend the response asked for,
        /// or <see langword="null" />.
        /// </summary>
        public string? RefusedResendMessage { get; init; }

        /// <summary>
        /// Gets the transfer's retries on a fresh connection so far, this one included, as
        /// <c>-v</c> counts them when <see cref="DiedBeforeResponse" />.
        /// </summary>
        public int RetryCount { get; init; } = 1;

        /// <summary>
        /// Gets a value indicating whether the connection is reported left intact although the
        /// server closed it to end the body (ADR-0324); it is marked reusable all the same, and
        /// the pool finds it dead (ADR-0112).
        /// </summary>
        public bool LeftIntactAfterServerClosed { get; init; }

        /// <summary>
        /// Gets the HTTP/2 session the exchange's connection switched to after an h2c upgrade's
        /// <c>101</c> (BL-866), or <see langword="null" /> when it did not switch.
        /// </summary>
        public Http2Session? UpgradedSession { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the exchange ran on a stream of an HTTP/2
        /// connection, which a transfer that times out or that <c>-f</c> fails leaves intact
        /// (BL-5974, BL-5977).
        /// </summary>
        public bool RanOnHttp2Stream { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the exchange ran on a stream of an HTTP/3
        /// connection, which a transfer that times out leaves intact (BL-5978).
        /// </summary>
        public bool RanOnHttp3Stream { get; set; }

        /// <summary>
        /// Gets a value indicating whether the exchange timed out on an HTTP/2 or HTTP/3 stream,
        /// or <c>-f</c> failed it before its body on an HTTP/2 stream: curl ends only the stream,
        /// reports the connection left intact and keeps it for the next transfer. No retry may
        /// follow on it, so it does not count towards <see cref="ReportsLeftIntact" />.
        /// </summary>
        public bool EndedOnlyItsStream =>
            !FailedAfterBody && Result.ExitCode switch
            {
                CurlExitCode.OperationTimedOut => RanOnHttp2Stream || RanOnHttp3Stream,
                CurlExitCode.HttpReturnedError => RanOnHttp2Stream,
                _ => false,
            };

        /// <summary>
        /// Gets a value indicating whether the connection is reported left intact: it
        /// <see cref="KeepsAlive" />, or it is <see cref="LeftIntactAfterServerClosed" />.
        /// </summary>
        public bool ReportsLeftIntact => KeepsAlive || LeftIntactAfterServerClosed;

        /// <summary>Gets the result, with the report so far.</summary>
        public TransferResult Result { get; } = Result;

        /// <summary>
        /// Gets the request to send next, or <see langword="null" /> when the result is final.
        /// </summary>
        public HttpRequestPlan? Retry { get; } = Retry;

        /// <summary>
        /// Gets a value indicating whether the retry may be sent on the same connection.
        /// </summary>
        public bool KeepsAlive { get; } = KeepsAlive;
    }
}
