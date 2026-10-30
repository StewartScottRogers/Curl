using System.Net.Sockets;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>
/// A Hyper-V socket address: the VM ID and the service ID, carried in <c>SOCKADDR_HV</c> as Microsoft's "Make your own
/// integration services" defines it.
/// </summary>
/// <param name="VmId">The partition to connect to, or one of the well-known IDs such as <see cref="Parent" />.</param>
/// <param name="ServiceId">The service registered under <c>GuestCommunicationServices</c>.</param>
public sealed record HyperVSocketAddress(Guid VmId, Guid ServiceId) : VirtualMachineSocketAddress
{
    /// <summary><c>AF_HYPERV</c>.</summary>
    public const AddressFamily AddressFamilyHyperV = (AddressFamily)34;

    /// <summary><c>HV_PROTOCOL_RAW</c>.</summary>
    public const ProtocolType ProtocolRaw = (ProtocolType)1;

    /// <summary>The size of <c>SOCKADDR_HV</c>.</summary>
    public const int SocketAddressSize = 36;

    /// <summary>Gets <c>HV_GUID_PARENT</c>, the parent partition: from a VM, its host.</summary>
    public static Guid Parent { get; } = new("a42e7cda-d03f-480c-9cc2-a4de20abb878");

    /// <summary>Gets <c>HV_GUID_LOOPBACK</c>, this partition.</summary>
    public static Guid Loopback { get; } = new("e0e16197-dd56-4a10-9195-5ee7a155a838");

    /// <summary>Gets <c>HV_GUID_SILOHOST</c>, the host of a hypervisor-isolated container.</summary>
    public static Guid SiloHost { get; } = new("36bd0c5c-7276-4223-88ba-7d03b654c568");

    /// <inheritdoc />
    public override AddressFamily Family => AddressFamilyHyperV;

    /// <inheritdoc />
    public override ProtocolType Protocol => ProtocolRaw;

    /// <summary>Gives <c>SOCKADDR_HV</c>: <c>Family</c>, <c>Reserved</c>, <c>VmId</c> and <c>ServiceId</c>, each GUID as Windows lays it out.</summary>
    /// <returns>The 36 bytes.</returns>
    public override byte[] ToSocketAddressBytes()
    {
        byte[] bytes = new byte[SocketAddressSize];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 2), (ushort)AddressFamilyHyperV);
        VmId.TryWriteBytes(bytes.AsSpan(4, 16));
        ServiceId.TryWriteBytes(bytes.AsSpan(20, 16));
        return bytes;
    }

    /// <summary>Gives the address as <c>--vm-socket</c> takes it.</summary>
    /// <returns><c>Hyper-V socket &lt;vm&gt;:&lt;service&gt;</c>.</returns>
    public override string ToString() => $"Hyper-V socket {VmId:D}:{ServiceId:D}";

    /// <summary>Parses a VM and a service; the all-zero wildcard, which only <c>bind</c> takes, is refused for either.</summary>
    /// <param name="vm">The VM: a GUID, <c>parent</c>, <c>loopback</c> or <c>silohost</c>.</param>
    /// <param name="service">The service, a GUID.</param>
    /// <returns>The address, or <see langword="null" /> when either part is not one.</returns>
    internal static HyperVSocketAddress? Parse(string vm, string service)
    {
        Guid vmId = ParseVm(vm);
        Guid serviceId = Guid.TryParse(service, out Guid parsed) ? parsed : Guid.Empty;
        return vmId != Guid.Empty && serviceId != Guid.Empty ? new HyperVSocketAddress(vmId, serviceId) : null;
    }

    private static Guid ParseVm(string text) => text.ToLowerInvariant() switch
    {
        "parent" => Parent,
        "loopback" => Loopback,
        "silohost" => SiloHost,
        _ => Guid.TryParse(text, out Guid vmId) ? vmId : Guid.Empty,
    };
}
