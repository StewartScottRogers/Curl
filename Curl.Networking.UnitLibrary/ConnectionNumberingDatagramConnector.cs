using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Opens channels through <paramref name="connector" /> and gives each open the next number in
/// <paramref name="cache" />'s sequence, the one the TCP connections over that cache are numbered
/// in, a failed open's included: curl 8.21.0 numbers a TFTP transfer's connection with the
/// connections before it, so <c>curl -v tftp://h/a tftp://h/b</c> shuts down <c>#0</c> and then
/// <c>#1</c>, and a TFTP URL after an HTTP one shuts down <c>#1</c> (measured, BL-969; ADR-0109).
/// </summary>
/// <param name="connector">Opens the channels.</param>
/// <param name="cache">The run's connections, whose next number each open takes.</param>
internal sealed class ConnectionNumberingDatagramConnector(IDatagramConnector connector, ConnectionCache cache) : IDatagramConnector
{
    /// <summary>
    /// Opens a channel as the wrapped connector does and returns its result with the next
    /// connection number.
    /// </summary>
    /// <param name="host">The server's host name or address.</param>
    /// <param name="port">The server's port.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open's result, numbered.</returns>
    public async ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        var opened = await connector.OpenAsync(host, port, cancellationToken).ConfigureAwait(false);
        return opened.WithConnectionNumber(cache.NumberNextConnection());
    }
}
