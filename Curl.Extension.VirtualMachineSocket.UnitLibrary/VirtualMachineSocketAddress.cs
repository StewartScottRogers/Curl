using System.Globalization;
using System.Net.Sockets;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>
/// Where <c>--vm-socket</c> connects: a vsock(7) CID and port (<see cref="VsockAddress" />) or a Hyper-V socket's VM and
/// service GUIDs (<see cref="HyperVSocketAddress" />), with the socket address structure its socket is connected to.
/// </summary>
public abstract record VirtualMachineSocketAddress
{
    /// <summary>Gets the address family the socket is opened in: <c>AF_VSOCK</c> or <c>AF_HYPERV</c>.</summary>
    public abstract AddressFamily Family { get; }

    /// <summary>Gets the protocol the socket is opened with: 0 for vsock, <c>HV_PROTOCOL_RAW</c> for Hyper-V.</summary>
    public abstract ProtocolType Protocol { get; }

    /// <summary>Gives the socket address structure, <c>sockaddr_vm</c> or <c>SOCKADDR_HV</c>, in this machine's byte order.</summary>
    /// <returns>The structure's bytes, starting with its two-byte address family.</returns>
    public abstract byte[] ToSocketAddressBytes();

    /// <summary>
    /// Parses <paramref name="text" />: <c>&lt;cid&gt;:&lt;port&gt;</c> for vsock, the CID a number or <c>hypervisor</c>,
    /// <c>local</c> or <c>host</c>; or <c>&lt;vm&gt;:&lt;service&gt;</c> for Hyper-V, the VM a GUID or <c>parent</c>,
    /// <c>loopback</c> or <c>silohost</c> and the service a GUID.
    /// </summary>
    /// <param name="text">The text, such as <c>3:5000</c> or <c>parent:3049197c-facb-11e6-bd58-64006a7986d3</c>.</param>
    /// <param name="address">The address, or <see langword="null" /> when the text is not one.</param>
    /// <returns><see langword="true" /> when <paramref name="text" /> is an address.</returns>
    public static bool TryParse(string text, out VirtualMachineSocketAddress? address)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] parts = text.Split(':');
        address = parts.Length == 2 ? (VirtualMachineSocketAddress?)VsockAddress.Parse(parts[0], parts[1]) ?? HyperVSocketAddress.Parse(parts[0], parts[1]) : null;
        return address is not null;
    }

    /// <summary>Parses a decimal 32-bit number with no sign, spaces or separators.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number, or <see langword="null" /> when the text is not one.</returns>
    internal static uint? ParseNumber(string text) =>
        uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint number) ? number : null;
}
