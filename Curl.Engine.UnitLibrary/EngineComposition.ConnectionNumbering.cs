using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>
    /// The datagram connector TFTP opens its channel through: over a
    /// <see cref="PoolingConnector" />, one that numbers each open in that pool's sequence, as
    /// curl 8.21.0 numbers a TFTP transfer's connection with the connections before it
    /// (BL-969); over any other connector, <paramref name="datagramConnector" /> itself.
    /// </summary>
    /// <param name="connector">The connector the TCP protocols connect through.</param>
    /// <param name="datagramConnector">Opens the channels.</param>
    /// <returns>The datagram connector.</returns>
    internal static IDatagramConnector NumberedDatagramsOf(IConnector connector, IDatagramConnector datagramConnector) =>
        connector is PoolingConnector pool ? pool.NumberingDatagrams(datagramConnector) : datagramConnector;

    /// <summary>
    /// The count the <c>file://</c> handler numbers its transfers in: over a
    /// <see cref="PoolingConnector" />, that pool's, as curl 8.21.0 numbers a <c>file://</c>
    /// transfer with every connection the run opens (BL-977); over any other connector, a count
    /// of its own from <c>0</c>.
    /// </summary>
    /// <param name="connector">The connector the TCP protocols connect through.</param>
    /// <returns>The count.</returns>
    internal static IConnectionNumbers ConnectionNumbersOf(IConnector connector) =>
        connector is PoolingConnector pool ? pool.ConnectionNumbers : new ConnectionNumberSequence();
}
