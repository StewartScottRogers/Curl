using System.Net.Sockets;

namespace Curl.Extension.QuicMigration;

/// <summary>Tells a socket failure that means the local end of the network path is gone from any other failure.</summary>
internal static class LocalPathLoss
{
    /// <summary>
    /// Says whether <paramref name="exception" /> means the channel's local address or network went away (an interface
    /// down, a Wi-Fi to cellular change, an address withdrawn), the change RFC 9000 section 9.2 lets a client answer by
    /// migrating. A failure the peer caused (port unreachable, a reset) is not one, so it ends the transfer as before.
    /// </summary>
    /// <param name="exception">What a send or receive threw.</param>
    /// <returns><see langword="true" /> when moving to a new local address may help.</returns>
    public static bool Means(Exception exception) =>
        exception is SocketException { SocketErrorCode: SocketError.AddressNotAvailable or SocketError.NetworkDown or SocketError.NetworkUnreachable or SocketError.NetworkReset or SocketError.HostUnreachable };
}
