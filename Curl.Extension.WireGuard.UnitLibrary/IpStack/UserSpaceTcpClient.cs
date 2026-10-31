using System.Net;
using System.Security.Cryptography;

namespace Curl.Extension.WireGuard.IpStack;

/// <summary>Opens a TCP connection inside the tunnel from a random ephemeral port with a random initial sequence number.</summary>
public static class UserSpaceTcpClient
{
    /// <summary>The first port of the dynamic range (RFC 6335 section 6).</summary>
    public const int FirstEphemeralPort = 49152;

    /// <summary>Connects to <paramref name="remote" /> and gives the connection as a stream.</summary>
    /// <param name="channel">Where the IP packets go and come from.</param>
    /// <param name="localAddress">The client's address inside the tunnel, of the same family as <paramref name="remote" />.</param>
    /// <param name="remote">The server's address and port.</param>
    /// <param name="maximumSegmentSize">The Maximum Segment Size to offer.</param>
    /// <param name="timeProvider">The clock the retransmission timer runs on.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The established connection as a stream.</returns>
    public static async Task<Stream> ConnectAsync(
        IIpPacketChannel channel,
        IPAddress localAddress,
        IPEndPoint remote,
        ushort maximumSegmentSize,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        IPEndPoint local = new(localAddress, RandomNumberGenerator.GetInt32(FirstEphemeralPort, IPEndPoint.MaxPort + 1));
        var initialSequenceNumber = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));
        TcpConnectionParameters parameters = new(local, remote, initialSequenceNumber, maximumSegmentSize);
        return new TcpConnectionStream(await TcpConnection.ConnectAsync(channel, parameters, timeProvider, cancellationToken));
    }
}
