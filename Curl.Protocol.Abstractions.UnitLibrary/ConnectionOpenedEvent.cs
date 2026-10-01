using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// The facts about a newly established connection, reported through
/// <see cref="ITransferEvents.ReportConnectionOpened" /> (ADR-0046).
/// </summary>
public sealed record ConnectionOpenedEvent
{
    /// <summary>
    /// Gets the name connected to, as in the URL or the proxy; for a Unix domain socket, the
    /// socket's path, as curl 8.21.0 names it (measured, BL-507).
    /// </summary>
    public required string HostName { get; init; }

    /// <summary>
    /// Gets the address and port connected to; <c>0.0.0.0</c> port <c>0</c> for a Unix domain
    /// socket, which <see cref="UnixSocketRemoteIp" /> describes instead.
    /// </summary>
    public required IPEndPoint RemoteEndPoint { get; init; }

    /// <summary>
    /// Gets the local address and port the connection was made from; <c>0.0.0.0</c> port
    /// <c>0</c> for a Unix domain socket, which has neither.
    /// </summary>
    public required IPEndPoint LocalEndPoint { get; init; }

    /// <summary>
    /// Gets, for a connection through a Unix domain socket (<c>--unix-socket</c>,
    /// <c>--abstract-unix-socket</c>), the text curl 8.21.0 shows in place of the remote
    /// address: the path cut to 45 characters, empty for an abstract name. With it set, curl's
    /// line names this with port <c>0</c> and an empty local address with port <c>0</c>, and
    /// <see cref="RemoteEndPoint" /> and <see cref="LocalEndPoint" /> carry no meaning.
    /// <see langword="null" /> for a TCP connection.
    /// </summary>
    public string? UnixSocketRemoteIp { get; init; }

    /// <summary>
    /// Gets whether the connection is a transfer's second one, such as FTP's passive data
    /// connection, which curl 8.21.0's <c>-v</c> calls <c>Established 2nd connection</c>
    /// (measured, BL-944); <see langword="false" />, the default, for every other connection.
    /// </summary>
    public bool IsSecondConnection { get; init; }

    /// <summary>
    /// Gets curl's number for the connection, the <c>N</c> of <c>#N</c>.
    /// </summary>
    public required long ConnectionNumber { get; init; }
}
