namespace Curl.Protocol.Abstractions;

/// <summary>
/// Opens an <see cref="IDatagramChannel" /> to a host and port: the seam TFTP, the one
/// datagram protocol, acquires its transport through, once per transfer.
/// </summary>
/// <remarks>
/// TFTP cannot use <see cref="IConnector" />: datagram boundaries carry meaning, and the
/// server answers from a new port, its transfer identifier (RFC 1350 section 4), which a
/// byte stream cannot express (ADR-0005). The handler owns and disposes the channel it is
/// given, and never constructs a <see cref="System.Net.Sockets.Socket" /> itself.
/// </remarks>
public interface IDatagramConnector
{
    /// <summary>
    /// Resolves <paramref name="host" /> and opens a datagram channel whose
    /// <see cref="IDatagramChannel.ServerEndPoint" /> is <paramref name="host" /> at
    /// <paramref name="port" />.
    /// </summary>
    /// <param name="host">The host name or address literal from the transfer's URL.</param>
    /// <param name="port">The UDP port the first datagram goes to.</param>
    /// <param name="cancellationToken">Cancels the resolve and open.</param>
    /// <returns>
    /// <see cref="DatagramOpenResult.Opened(IDatagramChannel)" /> with the open channel,
    /// or <see cref="DatagramOpenResult.Failed(CurlExitCode, string)" /> carrying curl's
    /// exit code and the message curl prints: <see cref="CurlExitCode.CouldntResolveHost" />
    /// (6) when the host does not resolve, and <see cref="CurlExitCode.CouldntConnect" />
    /// (7) when no channel can be opened.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled. This is the only exception an
    /// implementation may let escape; every resolve or open failure is returned as a
    /// failed result instead.
    /// </exception>
    ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken);
}
