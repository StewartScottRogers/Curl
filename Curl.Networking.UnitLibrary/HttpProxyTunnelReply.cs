namespace Curl.Networking;

/// <summary>
/// What <see cref="HttpProxyTunnel.ReadReplyAsync" /> read from the proxy: the status code
/// of a complete reply, or the exit 56 message for one curl gives up on.
/// </summary>
/// <param name="StatusCode">
/// The status code on the reply's first line, <c>0</c> when it is not an HTTP status line;
/// <c>0</c> as well when <paramref name="RecvErrorMessage" /> is set.
/// </param>
/// <param name="RecvErrorMessage">
/// The message for <see cref="Protocol.Abstractions.CurlExitCode.RecvError" /> (56) when the
/// reply was cut short or too large, else <see langword="null" />.
/// </param>
internal readonly record struct HttpProxyTunnelReply(int StatusCode, string? RecvErrorMessage)
{
    /// <summary>
    /// Gets the status code on the reply's first line, or <c>0</c>.
    /// </summary>
    public int StatusCode { get; } = StatusCode;

    /// <summary>
    /// Gets the exit 56 message, or <see langword="null" /> for a complete reply.
    /// </summary>
    public string? RecvErrorMessage { get; } = RecvErrorMessage;

    /// <summary>
    /// Gets a value indicating whether the proxy opened the tunnel: a complete reply with a
    /// status from 200 to 299, as curl 8.21.0 accepts (measured: 299 opens it, 300 does not).
    /// </summary>
    public bool OpensTunnel => RecvErrorMessage is null && StatusCode is >= 200 and <= 299;

    /// <summary>
    /// Creates the reply curl gives up on, with its exit 56 message.
    /// </summary>
    /// <param name="recvErrorMessage">The message curl prints.</param>
    /// <returns>A reply with status code <c>0</c> and the message.</returns>
    public static HttpProxyTunnelReply Failed(string recvErrorMessage) => new(0, recvErrorMessage);
}
