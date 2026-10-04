using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>What <see cref="WsContentLength.Check" /> found in a refused reply's head.</summary>
/// <param name="AcceptedLength">
/// How many bytes of the head curl writes and reports: all of it, or up to the failing header line.
/// </param>
/// <param name="OverflowLineStarts">Where each header line that gets the overflow line before it starts.</param>
/// <param name="FailureExitCode">The exit code of a failure; <see cref="CurlExitCode.Ok" /> for none.</param>
/// <param name="FailureMessage">The failure's message, or <see langword="null" /> when the head is accepted.</param>
internal sealed record WsContentLengthCheck(
    int AcceptedLength,
    IReadOnlyList<int> OverflowLineStarts,
    CurlExitCode FailureExitCode,
    string? FailureMessage)
{
    /// <summary>A head accepted whole, with no overflow lines: a <c>101</c>, or <c>--ignore-content-length</c>.</summary>
    /// <param name="headLength">The head's length.</param>
    /// <returns>The check.</returns>
    internal static WsContentLengthCheck Unchecked(int headLength) => Accepted(headLength, []);

    /// <summary>A head accepted whole.</summary>
    /// <param name="headLength">The head's length.</param>
    /// <param name="overflowLineStarts">Where each overflowing header line starts.</param>
    /// <returns>The check.</returns>
    internal static WsContentLengthCheck Accepted(int headLength, IReadOnlyList<int> overflowLineStarts) =>
        new(headLength, overflowLineStarts, CurlExitCode.Ok, null);
}
