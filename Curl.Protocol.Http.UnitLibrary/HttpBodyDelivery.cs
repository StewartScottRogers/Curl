namespace Curl.Protocol.Http;

/// <summary>
/// What becomes of a final response's body once its head is read: written to the output, or
/// left unread because <c>-z</c> or <c>-C</c> says there is nothing to deliver
/// (<see cref="HttpDownloadConditions" />).
/// </summary>
internal enum HttpBodyDelivery
{
    /// <summary>
    /// The body is read and written to the output.
    /// </summary>
    Deliver = 0,

    /// <summary>
    /// The <c>-z</c>/<c>--time-cond</c> condition is not met: a 304, or a response whose
    /// <c>Last-Modified</c> fails the condition. No body is written and the transfer succeeds
    /// with the condition marked unmet, reported as a 304 as curl 8.21.0 does.
    /// </summary>
    TimeConditionUnmet,

    /// <summary>
    /// A <c>-C</c>/<c>--continue-at</c> resume has nothing left to fetch: a 416, or a response
    /// whose Content-Length is exactly the resume offset. No body is written and the transfer
    /// succeeds.
    /// </summary>
    NothingLeftToResume,
}
