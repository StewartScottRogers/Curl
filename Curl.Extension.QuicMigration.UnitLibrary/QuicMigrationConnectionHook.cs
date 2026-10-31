using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.QuicMigration;

/// <summary>
/// The <c>--quic-migrate</c> connection hook: curl's stream connector is kept unchanged, and curl's datagram connector
/// is wrapped so each channel it opens can move to a new local address (RFC 9000 section 9.2).
/// </summary>
internal sealed class QuicMigrationConnectionHook : IConnectionHook
{
    /// <inheritdoc />
    public ExtensionOption Option => QuicMigrationExtension.Option;

    /// <inheritdoc />
    public IConnector ConnectorOver(IConnector curlConnector) => curlConnector;

    /// <inheritdoc />
    public IDatagramConnector DatagramConnectorOver(IDatagramConnector curlDatagramConnector) =>
        new MigratingDatagramConnector(curlDatagramConnector);
}
