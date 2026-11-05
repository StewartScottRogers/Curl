using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>Opens a vsock or Hyper-V stream socket with <see cref="Socket" /> and connects it to the address's end point.</summary>
/// <param name="createSocket">Creates the unconnected stream socket for an address.</param>
/// <param name="connectSocket">Connects the socket to the address's end point, blocking until it is connected.</param>
public sealed class SystemVirtualMachineSocketOpener(
    Func<VirtualMachineSocketAddress, Socket> createSocket,
    Action<Socket, VirtualMachineSocketAddress> connectSocket) : IVirtualMachineSocketOpener
{
    /// <summary>Initializes an opener of real vsock and Hyper-V sockets.</summary>
    public SystemVirtualMachineSocketOpener()
        : this(CreateSocket, ConnectSocket)
    {
    }

    /// <inheritdoc />
    public async ValueTask<Stream> OpenAsync(VirtualMachineSocketAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        Socket socket = createSocket(address);
        try
        {
            await ConnectAsync(socket, address, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Connects <paramref name="socket" /> on the thread pool; closing the socket ends a connect that is cancelled.</summary>
    private async Task ConnectAsync(Socket socket, VirtualMachineSocketAddress address, CancellationToken cancellationToken)
    {
        await using (cancellationToken.Register(socket.Dispose).ConfigureAwait(false))
        {
            await Task.Run(() => connectSocket(socket, address), cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>Creates the address family's unconnected stream socket.</summary>
    // Excluded per ADR-0083: an AF_VSOCK or AF_HYPERV socket can only be created on a host that offers the family.
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: needs a host that offers AF_VSOCK or AF_HYPERV.")]
    private static Socket CreateSocket(VirtualMachineSocketAddress address) => new(address.Family, SocketType.Stream, address.Protocol);

    /// <summary>Connects <paramref name="socket" /> to the address's end point with <see cref="Socket.Connect(System.Net.EndPoint)" />.</summary>
    // Socket.ConnectAsync binds an unbound socket of a family it does not know to the IPv6 wildcard before ConnectEx on
    // Windows, which AF_HYPERV refuses (EINVAL); Connect goes straight to connect(2).
    // Excluded per ADR-0083: connecting needs a live vsock or Hyper-V peer, which only a virtual machine can offer.
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: needs a live vsock or Hyper-V peer.")]
    private static void ConnectSocket(Socket socket, VirtualMachineSocketAddress address) => socket.Connect(new VirtualMachineSocketEndPoint(address));
}
