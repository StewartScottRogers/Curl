namespace Curl.Protocol.Abstractions;

/// <summary>
/// The facts about an existing connection being reused, reported through
/// <see cref="ITransferEvents.ReportConnectionReused" /> (ADR-0046).
/// </summary>
public sealed record ConnectionReusedEvent
{
    /// <summary>
    /// Gets the scheme the connection was pooled under, such as <c>http</c> or
    /// <c>https</c>, the <c>&lt;scheme&gt;</c> of curl's
    /// <c>Reusing existing &lt;scheme&gt;: connection with host|proxy &lt;name&gt;</c> (ADR-0050).
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="HostName" /> names a proxy rather than the
    /// origin, which makes curl say <c>with proxy</c> instead of <c>with host</c> (ADR-0050).
    /// </summary>
    public required bool IsProxy { get; init; }

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
