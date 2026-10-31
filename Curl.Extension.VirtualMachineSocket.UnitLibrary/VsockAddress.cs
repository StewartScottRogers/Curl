using System.Net.Sockets;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>
/// A vsock(7) address: a context identifier (CID) and a port, carried in <c>struct sockaddr_vm</c> in host byte order.
/// </summary>
/// <param name="Cid">The context identifier of the machine to connect to.</param>
/// <param name="Port">The port on that machine.</param>
public sealed record VsockAddress(uint Cid, uint Port) : VirtualMachineSocketAddress
{
    /// <summary><c>AF_VSOCK</c> on Linux.</summary>
    public const AddressFamily AddressFamilyVsock = (AddressFamily)40;

    /// <summary><c>VMADDR_CID_HYPERVISOR</c>, the hypervisor.</summary>
    public const uint HypervisorCid = 0;

    /// <summary><c>VMADDR_CID_LOCAL</c>, this machine's loopback.</summary>
    public const uint LocalCid = 1;

    /// <summary><c>VMADDR_CID_HOST</c>, the host of this virtual machine.</summary>
    public const uint HostCid = 2;

    /// <summary><c>VMADDR_CID_ANY</c> and <c>VMADDR_PORT_ANY</c>, the wildcard only <c>bind</c> takes.</summary>
    public const uint Any = uint.MaxValue;

    /// <summary>The size of <c>struct sockaddr_vm</c>.</summary>
    public const int SocketAddressSize = 16;

    /// <inheritdoc />
    public override AddressFamily Family => AddressFamilyVsock;

    /// <inheritdoc />
    public override ProtocolType Protocol => 0;

    /// <summary>
    /// Gives <c>struct sockaddr_vm</c>: <c>svm_family</c>, <c>svm_reserved1</c>, <c>svm_port</c>, <c>svm_cid</c> and
    /// four zero bytes.
    /// </summary>
    /// <returns>The 16 bytes.</returns>
    public override byte[] ToSocketAddressBytes()
    {
        byte[] bytes = new byte[SocketAddressSize];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 2), (ushort)AddressFamilyVsock);
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), Port);
        BitConverter.TryWriteBytes(bytes.AsSpan(8, 4), Cid);
        return bytes;
    }

    /// <summary>Gives the address as <c>--vm-socket</c> takes it.</summary>
    /// <returns><c>vsock &lt;cid&gt;:&lt;port&gt;</c>.</returns>
    public override string ToString() => $"vsock {Cid}:{Port}";

    /// <summary>Parses a CID and a port; the wildcard is refused, as only <c>bind</c> takes it.</summary>
    /// <param name="cid">The CID: a number, <c>hypervisor</c>, <c>local</c> or <c>host</c>.</param>
    /// <param name="port">The port, a number.</param>
    /// <returns>The address, or <see langword="null" /> when either part is not one.</returns>
    internal static VsockAddress? Parse(string cid, string port)
    {
        uint? cidNumber = ParseCid(cid);
        uint? portNumber = ParseNumber(port);
        return cidNumber is { } c and not Any && portNumber is { } p and not Any ? new VsockAddress(c, p) : null;
    }

    private static uint? ParseCid(string text) => text.ToLowerInvariant() switch
    {
        "hypervisor" => HypervisorCid,
        "local" => LocalCid,
        "host" => HostCid,
        _ => ParseNumber(text),
    };
}
