namespace Curl.Extension.VirtualMachineSocket;

/// <summary>Opens a connected vsock or Hyper-V stream socket as a byte stream.</summary>
public interface IVirtualMachineSocketOpener
{
    /// <summary>Connects to <paramref name="address" />.</summary>
    /// <param name="address">The vsock or Hyper-V socket address.</param>
    /// <param name="cancellationToken">Cancels the connect.</param>
    /// <returns>The connected socket's stream.</returns>
    /// <exception cref="System.Net.Sockets.SocketException">The platform has no such socket family, or the peer refused or did not answer.</exception>
    ValueTask<Stream> OpenAsync(VirtualMachineSocketAddress address, CancellationToken cancellationToken);
}
