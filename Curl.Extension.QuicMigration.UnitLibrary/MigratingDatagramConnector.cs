using Curl.Protocol.Abstractions;

namespace Curl.Extension.QuicMigration;

/// <summary>
/// Opens each datagram channel through curl's datagram connector and wraps it in a <see cref="MigratingDatagramChannel" />,
/// which opens a fresh one through the same connector when the path's local address is lost.
/// </summary>
/// <param name="curlDatagramConnector">The datagram connector curl would use.</param>
internal sealed class MigratingDatagramConnector(IDatagramConnector curlDatagramConnector) : IDatagramConnector
{
    /// <summary>Gets the connector every channel, first and migrated, opens through.</summary>
    public IDatagramConnector CurlDatagramConnector { get; } = curlDatagramConnector ?? throw new ArgumentNullException(nameof(curlDatagramConnector));

    /// <inheritdoc />
    public async ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        DatagramOpenResult result = await CurlDatagramConnector.OpenAsync(host, port, cancellationToken).ConfigureAwait(false);
        return result.Channel is null
            ? result
            : DatagramOpenResult.Opened(new MigratingDatagramChannel(result.Channel, CurlDatagramConnector, host, port)).WithConnectionNumber(result.ConnectionNumber);
    }
}
