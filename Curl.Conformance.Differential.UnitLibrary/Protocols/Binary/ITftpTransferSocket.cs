namespace Curl.Conformance.Differential.Protocols.Binary;

/// <summary>
/// The fresh UDP socket proto2_servers.py's <c>Tftp</c> class opens per request (bound to an ephemeral port, two
/// seconds to wait for a datagram), as the one seam <see cref="TftpResponder"/> needs, so its packet logic runs
/// over byte arrays.
/// </summary>
public interface ITftpTransferSocket
{
    /// <summary>Sends <paramref name="datagram"/> to the client that made the request.</summary>
    Task SendAsync(byte[] datagram, CancellationToken cancellationToken);

    /// <summary>Waits for the next datagram from anyone; <see langword="null"/> means none came in time or receiving failed.</summary>
    Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken);
}
