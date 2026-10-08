using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// What <see cref="HttpProxyTunnel.ReadReplyAsync(Protocol.Abstractions.IConnection, bool, CancellationToken)" /> read from the proxy: the status code
/// and the header fields curl acts on of a complete reply, or the exit code and message for one
/// curl gives up on.
/// </summary>
/// <param name="StatusCode">
/// The status code on the reply's first line, <c>0</c> when it is not an HTTP status line;
/// <c>0</c> as well when <paramref name="FailureMessage" /> is set.
/// </param>
/// <param name="FailureMessage">
/// The message curl prints when it gives up on the reply - cut short or too large, exit 56, or
/// a <c>Content-Length</c> that is not a number, exit 8 - else <see langword="null" />.
/// </param>
internal readonly record struct HttpProxyTunnelReply(int StatusCode, string? FailureMessage)
{
    private readonly IReadOnlyList<string>? _proxyAuthenticate;

    /// <summary>
    /// Gets the status code on the reply's first line, or <c>0</c>.
    /// </summary>
    public int StatusCode { get; } = StatusCode;

    /// <summary>
    /// Gets the message curl prints when it gives up on the reply, or <see langword="null" /> for a complete reply.
    /// </summary>
    public string? FailureMessage { get; } = FailureMessage;

    /// <summary>
    /// Gets the exit code that goes with <see cref="FailureMessage" />:
    /// <see cref="CurlExitCode.RecvError" /> (56) unless the reply says otherwise.
    /// </summary>
    public CurlExitCode FailureExitCode { get; init; } = CurlExitCode.RecvError;

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
    /// Gets how many header lines <see cref="Head" /> holds - its lines less the status line and
    /// the blank line that ends it - which curl 8.21.0 stores and counts toward its limit of
    /// 5000 response headers (measured, BL-1609 Notes); <c>0</c> for an empty head.
    /// </summary>
    public int HeaderCount => Math.Max(0, Head.Span.Count((byte)'\n') - 2);

    /// <summary>
    /// Gets a value indicating whether the body is <c>Transfer-Encoding: chunked</c>, so it ends
    /// with its last chunk and trailer rather than after <see cref="ContentLength" /> bytes.
    /// </summary>
    public bool IsChunked { get; init; }

    /// <summary>
    /// Gets a value indicating whether another CONNECT can follow on the same connection once
    /// the body is read - <see cref="ContentLength" /> bytes, or the chunked body when
    /// <see cref="IsChunked" />: no <c>Connection: close</c> or <c>Proxy-Connection: close</c>,
    /// as curl 8.21.0 closes on either and keeps the connection otherwise, chunked or not (BL-862 Notes).
    /// </summary>
    public bool LeavesConnectionReusable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the proxy opened the tunnel: a complete reply with a
    /// status from 200 to 299, as curl 8.21.0 accepts (measured: 299 opens it, 300 does not).
    /// </summary>
    public bool OpensTunnel => FailureMessage is null && StatusCode is >= 200 and <= 299;

    /// <summary>
    /// Gets a value indicating whether the proxy opened a CONNECT-UDP tunnel: a complete reply
    /// with status 101 or one from 200 to 299, as curl 8.22.0 accepts both a
    /// <c>101 Switching Protocols</c> and a <c>200 OK</c> (measured, BL-942).
    /// </summary>
    public bool OpensUdpTunnel => OpensTunnel || (FailureMessage is null && StatusCode == 101);

    /// <summary>
    /// Creates the reply curl gives up on with exit 56, with its message.
    /// </summary>
    /// <param name="failureMessage">The message curl prints.</param>
    /// <returns>A reply with status code <c>0</c> and the message.</returns>
    public static HttpProxyTunnelReply Failed(string failureMessage) => new(0, failureMessage);
}
