using System.Net;
using System.Net.Sockets;

namespace Curl.Extension.VirtualMachineSocket;

/// <summary>
/// Checks the <c>--vm-socket</c> address forms and their socket address structures against vsock(7) "Address format"
/// (<c>struct sockaddr_vm</c>, the well-known CIDs) and Microsoft's "Make your own integration services", "Hyper-V
/// socket address" (<c>SOCKADDR_HV</c>, the well-known VM IDs).
/// </summary>
[TestClass]
public sealed class VirtualMachineSocketAddressTests
{
    private static readonly Guid Service = new("3049197c-facb-11e6-bd58-64006a7986d3");

    [TestMethod]
    [DataRow("3:5000", 3u, 5000u, DisplayName = "a numeric CID")]
    [DataRow("hypervisor:1", 0u, 1u, DisplayName = "VMADDR_CID_HYPERVISOR")]
    [DataRow("local:1", 1u, 1u, DisplayName = "VMADDR_CID_LOCAL")]
    [DataRow("HOST:1024", 2u, 1024u, DisplayName = "VMADDR_CID_HOST, any case")]
    [DataRow("4294967294:4294967294", 4294967294u, 4294967294u, DisplayName = "the largest CID and port short of the wildcard")]
    public void TryParse_CidAndPort_IsAVsockAddress(string text, uint cid, uint port)
    {
        Assert.IsTrue(VirtualMachineSocketAddress.TryParse(text, out VirtualMachineSocketAddress? address));

        Assert.AreEqual(new VsockAddress(cid, port), address);
    }

    [TestMethod]
    [DataRow("parent", "a42e7cda-d03f-480c-9cc2-a4de20abb878", DisplayName = "HV_GUID_PARENT")]
    [DataRow("Loopback", "e0e16197-dd56-4a10-9195-5ee7a155a838", DisplayName = "HV_GUID_LOOPBACK, any case")]
    [DataRow("silohost", "36bd0c5c-7276-4223-88ba-7d03b654c568", DisplayName = "HV_GUID_SILOHOST")]
    [DataRow("{9a9cd5d2-0b2c-4f1b-8e3b-5a1f0e7d6c4b}", "9a9cd5d2-0b2c-4f1b-8e3b-5a1f0e7d6c4b", DisplayName = "a VM's own ID, braced")]
    public void TryParse_VmAndServiceGuid_IsAHyperVSocketAddress(string vm, string vmId)
    {
        Assert.IsTrue(VirtualMachineSocketAddress.TryParse($"{vm}:{Service}", out VirtualMachineSocketAddress? address));

        Assert.AreEqual(new HyperVSocketAddress(new Guid(vmId), Service), address);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("3", DisplayName = "no port")]
    [DataRow("3:5000:1", DisplayName = "three parts")]
    [DataRow("4294967295:5000", DisplayName = "VMADDR_CID_ANY")]
    [DataRow("3:4294967295", DisplayName = "VMADDR_PORT_ANY")]
    [DataRow("3:-1", DisplayName = "a signed port")]
    [DataRow(" 3:5000", DisplayName = "a space")]
    [DataRow("4294967296:1", DisplayName = "a CID past 32 bits")]
    [DataRow("guest:5000", DisplayName = "an unknown name")]
    [DataRow("parent:5000", DisplayName = "a Hyper-V VM with a port")]
    [DataRow("host:3049197c-facb-11e6-bd58-64006a7986d3", DisplayName = "a vsock CID with a GUID")]
    [DataRow("00000000-0000-0000-0000-000000000000:3049197c-facb-11e6-bd58-64006a7986d3", DisplayName = "HV_GUID_WILDCARD")]
    [DataRow("parent:00000000-0000-0000-0000-000000000000", DisplayName = "the all-zero service")]
    public void TryParse_TextThatIsNotAnAddress_IsRefused(string text)
    {
        Assert.IsFalse(VirtualMachineSocketAddress.TryParse(text, out VirtualMachineSocketAddress? address));

        Assert.IsNull(address);
    }

    [TestMethod]
    public void TryParse_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => VirtualMachineSocketAddress.TryParse(null!, out _));

    [TestMethod]
    public void VsockAddress_SocketAddressBytes_AreSockaddrVmInHostByteOrder()
    {
        VsockAddress address = new(3, 5000);

        byte[] bytes = address.ToSocketAddressBytes();

        // svm_family AF_VSOCK (40), svm_reserved1, svm_port 5000, svm_cid 3, svm_zero; little-endian hosts.
        CollectionAssert.AreEqual(new byte[] { 40, 0, 0, 0, 0x88, 0x13, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0 }, bytes);
        Assert.AreEqual((AddressFamily)40, address.Family);
        Assert.AreEqual((ProtocolType)0, address.Protocol);
        Assert.AreEqual("vsock 3:5000", address.ToString());
    }

    [TestMethod]
    public void HyperVSocketAddress_SocketAddressBytes_AreSockaddrHvWithGuidsAsWindowsLaysThemOut()
    {
        HyperVSocketAddress address = new(HyperVSocketAddress.Parent, Service);

        byte[] bytes = address.ToSocketAddressBytes();

        // Family AF_HYPERV (34), Reserved, VmId, ServiceId; each GUID's first three fields little-endian.
        byte[] expected =
        [
            34, 0, 0, 0,
            0xda, 0x7c, 0x2e, 0xa4, 0x3f, 0xd0, 0x0c, 0x48, 0x9c, 0xc2, 0xa4, 0xde, 0x20, 0xab, 0xb8, 0x78,
            0x7c, 0x19, 0x49, 0x30, 0xcb, 0xfa, 0xe6, 0x11, 0xbd, 0x58, 0x64, 0x00, 0x6a, 0x79, 0x86, 0xd3,
        ];
        CollectionAssert.AreEqual(expected, bytes);
        Assert.AreEqual((AddressFamily)34, address.Family);
        Assert.AreEqual((ProtocolType)1, address.Protocol);
        Assert.AreEqual("Hyper-V socket a42e7cda-d03f-480c-9cc2-a4de20abb878:3049197c-facb-11e6-bd58-64006a7986d3", address.ToString());
    }

    [TestMethod]
    [DataRow(false, DisplayName = "vsock")]
    [DataRow(true, DisplayName = "Hyper-V")]
    public void EndPoint_Serialize_CarriesTheStructureAfterItsFamily(bool hyperV)
    {
        VirtualMachineSocketAddress address = hyperV ? new HyperVSocketAddress(HyperVSocketAddress.Loopback, Service) : new VsockAddress(VsockAddress.HostCid, 22);
        VirtualMachineSocketEndPoint endPoint = new(address);

        SocketAddress socketAddress = endPoint.Serialize();

        byte[] bytes = address.ToSocketAddressBytes();
        Assert.AreEqual(bytes.Length, socketAddress.Size);
        CollectionAssert.AreEqual(bytes, Enumerable.Range(0, bytes.Length).Select(index => socketAddress[index]).ToArray());
        Assert.AreEqual(address.Family, endPoint.AddressFamily);
        Assert.AreSame(address, endPoint.Address);
        Assert.AreSame(endPoint, endPoint.Create(socketAddress));
        Assert.AreEqual(address.ToString(), endPoint.ToString());
    }
}
