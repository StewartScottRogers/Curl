using System.Net;
using System.Net.Sockets;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>
/// The end point a socket in <c>AF_VSOCK</c> or <c>AF_HYPERV</c> connects to: it serializes to the address's
/// <c>sockaddr_vm</c> or <c>SOCKADDR_HV</c>, which .NET has no end point of its own for.
/// </summary>
/// <param name="address">The address.</param>
public sealed class VirtualMachineSocketEndPoint(VirtualMachineSocketAddress address) : EndPoint
{
    /// <summary>Gets the address.</summary>
    public VirtualMachineSocketAddress Address => address;

    /// <inheritdoc />
    public override AddressFamily AddressFamily => address.Family;

    /// <summary>
    /// Gives the socket address: every byte of the structure, its two-byte family included, unchanged. It starts as an
    /// <see cref="AddressFamily.Unspecified" /> address because .NET on Linux and macOS refuses to build a
    /// <see cref="SocketAddress" /> for a family it does not know, such as <c>AF_VSOCK</c> or <c>AF_HYPERV</c>
    /// (ADR-1115).
    /// </summary>
    /// <returns>The socket address.</returns>
    public override SocketAddress Serialize()
    {
        byte[] bytes = address.ToSocketAddressBytes();
        SocketAddress socketAddress = new(AddressFamily.Unspecified, bytes.Length);
        for (int index = 0; index < bytes.Length; index++)
        {
            socketAddress[index] = bytes[index];
        }

        return socketAddress;
    }

    /// <summary>Gives this end point: the socket connects only to the address it was given.</summary>
    /// <param name="socketAddress">The address the socket reports; not read.</param>
    /// <returns>This end point.</returns>
    public override EndPoint Create(SocketAddress socketAddress) => this;

    /// <inheritdoc />
    public override string ToString() => address.ToString();
}
