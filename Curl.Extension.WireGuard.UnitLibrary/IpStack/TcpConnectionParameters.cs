using System.Net;

namespace Curl.Extension.WireGuard.IpStack;

/// <summary>What a <see cref="TcpConnection" /> is opened with.</summary>
/// <param name="Local">The client's address inside the tunnel and its port.</param>
/// <param name="Remote">The server's address and port; of the same family as <paramref name="Local" />.</param>
/// <param name="InitialSequenceNumber">The sequence number of the SYN.</param>
/// <param name="MaximumSegmentSize">The Maximum Segment Size the SYN offers: the tunnel's MTU less the IP and TCP headers.</param>
public sealed record TcpConnectionParameters(
    IPEndPoint Local,
    IPEndPoint Remote,
    uint InitialSequenceNumber,
    ushort MaximumSegmentSize);
