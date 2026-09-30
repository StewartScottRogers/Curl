namespace Curl.Networking;

/// <summary>
/// What <see cref="HttpProxyTunnel.ReadReplyAsync" /> read from the proxy: the status code
/// and the header fields curl acts on of a complete reply, or the exit 56 message for one
/// curl gives up on.
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
    private readonly IReadOnlyList<string>? _proxyAuthenticate;

    /// <summary>
    /// Gets the status code on the reply's first line, or <c>0</c>.
    /// </summary>
    public int StatusCode { get; } = StatusCode;

    /// <summary>
    /// Gets the exit 56 message, or <see langword="null" /> for a complete reply.
    /// </summary>
    public string? RecvErrorMessage { get; } = RecvErrorMessage;

    /// <summary>
    /// Gets the value of every <c>Proxy-Authenticate</c> field, verbatim apart from the blanks
    /// around it, in the order received; empty when there is none.
    /// </summary>
    public IReadOnlyList<string> ProxyAuthenticate
    {
        get => _proxyAuthenticate ?? [];
        init => _proxyAuthenticate = value;
    }

    /// <summary>
    /// Gets the length of the body after the header block, as the last valid
    /// <c>Content-Length</c> gives it; <c>0</c> when there is none.
    /// </summary>
    public long ContentLength { get; init; }

    /// <summary>
    /// Gets the reply's head exactly as read, from its status line to the blank line that
    /// ends it; empty for a reply curl gives up on.
    /// </summary>
    public ReadOnlyMemory<byte> Head { get; init; }

    /// <summary>
    /// Gets a value indicating whether another CONNECT can follow on the same connection once
    /// <see cref="ContentLength" /> body bytes are read: no <c>Connection: close</c> or
    /// <c>Proxy-Connection: close</c>, as curl 8.21.0 closes on either, and no chunked body.
    /// </summary>
    public bool LeavesConnectionReusable { get; init; }

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
