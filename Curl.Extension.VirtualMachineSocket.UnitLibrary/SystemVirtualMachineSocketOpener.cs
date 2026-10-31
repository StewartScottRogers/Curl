using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>Opens a vsock or Hyper-V stream socket with <see cref="Socket" /> and connects it to the address's end point.</summary>
// Excluded per ADR-0083: every line needs a live vsock or Hyper-V peer, which only a virtual machine can offer.
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin socket adapter; every line needs a live vsock or Hyper-V peer.")]
public sealed class SystemVirtualMachineSocketOpener : IVirtualMachineSocketOpener
{
    /// <inheritdoc />
    public async ValueTask<Stream> OpenAsync(VirtualMachineSocketAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        Socket socket = new(address.Family, SocketType.Stream, address.Protocol);
        try
        {
            // Socket.ConnectAsync binds an unbound socket of a family it does not know to the IPv6 wildcard before
            // ConnectEx on Windows, which AF_HYPERV refuses (EINVAL); Connect goes straight to connect(2). Closing the
            // socket ends a connect that is cancelled.
            await using (cancellationToken.Register(socket.Dispose).ConfigureAwait(false))
            {
                await Task.Run(() => socket.Connect(new VirtualMachineSocketEndPoint(address)), cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
