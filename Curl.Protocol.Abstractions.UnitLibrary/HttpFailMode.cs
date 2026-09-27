namespace Curl.Protocol.Abstractions;

/// <summary>
/// How an HTTP response with a status of 400 or above ends the transfer, per
/// <c>-f</c>/<c>--fail</c> and <c>--fail-with-body</c> (ADR-0014).
/// </summary>
/// <remarks>
/// Exit 22 is <see cref="CurlExitCode.HttpReturnedError" />.
/// </remarks>
public enum HttpFailMode
{
    /// <summary>
    /// The default: the body of a 4xx or 5xx response is written and the exit code is 0.
    /// </summary>
    None = 0,

    /// <summary>
    /// <c>-f</c>/<c>--fail</c>: no body is written and the exit code is 22.
    /// </summary>
    Fail,

    /// <summary>
    /// <c>--fail-with-body</c>: the body is written, then the exit code is 22.
    /// </summary>
    FailWithBody,
}
