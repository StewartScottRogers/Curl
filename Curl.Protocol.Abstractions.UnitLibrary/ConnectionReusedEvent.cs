namespace Curl.Protocol.Abstractions;

/// <summary>
/// The facts about an existing connection being reused, reported through
/// <see cref="ITransferEvents.ReportConnectionReused" /> (ADR-0046).
/// </summary>
public sealed record ConnectionReusedEvent
{
    /// <summary>
    /// Gets the name of the host the connection is to.
    /// </summary>
    public required string HostName { get; init; }

    /// <summary>
    /// Gets the port the connection is to.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Gets curl's number for the connection, the <c>N</c> of <c>#N</c>.
    /// </summary>
    public required long ConnectionNumber { get; init; }
}
