using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// The facts about a newly established connection, reported through
/// <see cref="ITransferEvents.ReportConnectionOpened" /> (ADR-0046).
/// </summary>
public sealed record ConnectionOpenedEvent
{
    /// <summary>
    /// Gets the name connected to, as in the URL or the proxy.
    /// </summary>
    public required string HostName { get; init; }

    /// <summary>
    /// Gets the address and port connected to.
    /// </summary>
    public required IPEndPoint RemoteEndPoint { get; init; }

    /// <summary>
    /// Gets the local address and port the connection was made from.
    /// </summary>
    public required IPEndPoint LocalEndPoint { get; init; }

    /// <summary>
    /// Gets curl's number for the connection, the <c>N</c> of <c>#N</c>.
    /// </summary>
    public required long ConnectionNumber { get; init; }
}
