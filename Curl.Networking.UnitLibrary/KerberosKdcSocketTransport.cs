using Curl.Kerberos;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="IKerberosKdcTransport" /> (ADR-0142, ADR-0176): a UDP datagram
/// through an <see cref="IDatagramConnector" />, answered within
/// <paramref name="replyTimeout" />, and a direct TCP connection through an
/// <see cref="IConnector" />. It only moves bytes: <c>Curl.Kerberos</c>'s KDC sender picks
/// UDP or TCP, switches to TCP on <c>KRB_ERR_RESPONSE_TOO_BIG</c> and frames TCP messages.
/// Every failure to reach the KDC is an <see cref="IOException" />, so the sender tries the
/// realm's next KDC.
/// </summary>
/// <param name="datagramConnector">Opens the UDP channel to a KDC.</param>
/// <param name="connector">Opens the TCP connection to a KDC, with no proxy and no TLS.</param>
/// <param name="replyTimeout">How long a UDP request waits for its reply: MIT's first wait is one second.</param>
/// <param name="timeProvider">Times the wait.</param>
public sealed class KerberosKdcSocketTransport(IDatagramConnector datagramConnector, IConnector connector, TimeSpan replyTimeout, TimeProvider timeProvider) : IKerberosKdcTransport
{
    /// <summary>The largest reply datagram read: the largest a UDP datagram can be.</summary>
    public const int MaximumDatagramLength = 65535;

    /// <inheritdoc />
    public async Task<byte[]> ExchangeDatagramAsync(string host, int port, ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    {
        DatagramOpenResult opened = await datagramConnector.OpenAsync(host, port, cancellationToken).ConfigureAwait(false);
        IDatagramChannel channel = opened.Channel ?? throw new IOException(opened.ErrorMessage);
        await using (channel.ConfigureAwait(false))
        {
            using CancellationTokenSource timeout = new(replyTimeout, timeProvider);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                await channel.SendAsync(request, channel.ServerEndPoint, linked.Token).ConfigureAwait(false);
                byte[] buffer = new byte[MaximumDatagramLength];
                DatagramReceived received = await channel.ReceiveAsync(buffer, linked.Token).ConfigureAwait(false);
                return buffer[..received.Length];
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new IOException($"The KDC {host} port {port} did not answer within {replyTimeout.TotalSeconds:0.###} s.");
            }
        }
    }

    /// <inheritdoc />
    public async Task<Stream> ConnectStreamAsync(string host, int port, CancellationToken cancellationToken)
    {
        ConnectResult connected = await connector.ConnectAsync(new ConnectTarget(host, port, UseTls: false), cancellationToken).ConfigureAwait(false);
        IConnection connection = connected.Connection ?? throw new IOException(connected.ErrorMessage);
        return new ConnectionStream(connection, ownsConnection: true);
    }
}
