using System.Net;
using Curl.Extension.Masque.Wire;
using Curl.HttpDatagram;

namespace Curl.Extension.Masque;

/// <summary>Checks the CONNECT-IP capsules of RFC 9484 section 4.7.</summary>
[TestClass]
public sealed class ConnectIpCapsuleCodecTests
{
    private static readonly IPAddress Documentation6 = IPAddress.Parse("2001:db8::1");

    [TestMethod]
    public void EncodeAddressAssign_WritesRequestIdVersionAddressAndPrefix()
    {
        var capsule = ConnectIpCapsuleCodec.EncodeAddressAssign([new(0, IPAddress.Parse("192.0.2.1"), 32), new(70, Documentation6, 64)]);

        Assert.AreEqual(ConnectIpCapsuleCodec.AddressAssignType, capsule.Type);
        byte[] expected = [0x00, 4, 192, 0, 2, 1, 32, 0x40, 70, 6, 0x20, 0x01, 0x0d, 0xb8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 64];
        CollectionAssert.AreEqual(expected, capsule.Value.ToArray());
    }

    [TestMethod]
    public void DecodeAddressAssign_ReadsBackWhatWasEncoded()
    {
        ConnectIpAddress[] addresses = [new(0, IPAddress.Parse("192.0.2.1"), 32), new(70, Documentation6, 128)];

        CollectionAssert.AreEqual(addresses, ConnectIpCapsuleCodec.DecodeAddressAssign(ConnectIpCapsuleCodec.EncodeAddressAssign(addresses)).ToArray());
    }

    [TestMethod]
    public void DecodeAddressAssign_Empty_WithdrawsEveryAddress() =>
        Assert.IsEmpty(ConnectIpCapsuleCodec.DecodeAddressAssign(ConnectIpCapsuleCodec.EncodeAddressAssign([])));

    [TestMethod]
    public void DecodeAddressRequest_ReadsBackWhatWasEncoded()
    {
        var capsule = ConnectIpCapsuleCodec.EncodeAddressRequest([new(1, IPAddress.Any, 0)]);

        Assert.AreEqual(ConnectIpCapsuleCodec.AddressRequestType, capsule.Type);
        CollectionAssert.AreEqual(new ConnectIpAddress[] { new(1, IPAddress.Any, 0) }, ConnectIpCapsuleCodec.DecodeAddressRequest(capsule).ToArray());
    }

    [TestMethod]
    public void DecodeAddressRequest_NoAddressOrRequestIdZero_IsMalformed()
    {
        Assert.AreEqual(
            "MASQUE proxy sent a malformed ADDRESS_REQUEST capsule: it holds no address or a request ID of 0",
            Assert.ThrowsExactly<MasqueException>(() => ConnectIpCapsuleCodec.DecodeAddressRequest(ConnectIpCapsuleCodec.EncodeAddressRequest([]))).Message);
        Assert.ThrowsExactly<MasqueException>(() => ConnectIpCapsuleCodec.DecodeAddressRequest(ConnectIpCapsuleCodec.EncodeAddressRequest([new(0, IPAddress.Any, 0)])));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x40 }, "ends inside a variable-length integer")]
    [DataRow(new byte[] { 0, 5, 1, 2, 3, 4, 32 }, "names IP version 5")]
    [DataRow(new byte[] { 0, 4, 192, 0, 2 }, "ends inside a field")]
    [DataRow(new byte[] { 0, 4, 192, 0, 2, 1 }, "ends inside a field")]
    [DataRow(new byte[] { 0, 4, 192, 0, 2, 0, 33 }, "gives prefix length 33 to 192.0.2.0")]
    [DataRow(new byte[] { 0, 6, 0x20, 1, 0xd, 0xb8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 129 }, "gives prefix length 129 to 2001:db8::")]
    public void DecodeAddressAssign_MalformedValue_IsRefused(byte[] value, string reason) =>
        Assert.AreEqual(
            $"MASQUE proxy sent a malformed ADDRESS_ASSIGN capsule: it {reason}",
            Assert.ThrowsExactly<MasqueException>(() => ConnectIpCapsuleCodec.DecodeAddressAssign(new Capsule(ConnectIpCapsuleCodec.AddressAssignType, value))).Message);

    [TestMethod]
    public void Decode_CapsuleOfAnotherType_IsRefused()
    {
        Capsule datagram = new(0, Array.Empty<byte>());

        StringAssert.Contains(Assert.ThrowsExactly<MasqueException>(() => ConnectIpCapsuleCodec.DecodeAddressAssign(datagram)).Message, "ADDRESS_ASSIGN capsule: it has capsule type 0");
        StringAssert.Contains(Assert.ThrowsExactly<MasqueException>(() => ConnectIpCapsuleCodec.DecodeAddressRequest(datagram)).Message, "ADDRESS_REQUEST");
        StringAssert.Contains(Assert.ThrowsExactly<MasqueException>(() => ConnectIpCapsuleCodec.DecodeRouteAdvertisement(datagram)).Message, "ROUTE_ADVERTISEMENT");
    }

    [TestMethod]
    public void Decode_NullCapsule_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ConnectIpCapsuleCodec.DecodeAddressAssign(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ConnectIpCapsuleCodec.DecodeRouteAdvertisement(null!));
    }

    [TestMethod]
    public void EncodeRouteAdvertisement_WritesVersionStartEndAndProtocol()
    {
        var capsule = ConnectIpCapsuleCodec.EncodeRouteAdvertisement([new(IPAddress.Parse("192.0.2.0"), IPAddress.Parse("192.0.2.255"), 17)]);

        Assert.AreEqual(ConnectIpCapsuleCodec.RouteAdvertisementType, capsule.Type);
        CollectionAssert.AreEqual(new byte[] { 4, 192, 0, 2, 0, 192, 0, 2, 255, 17 }, capsule.Value.ToArray());
    }

    [TestMethod]
    public void DecodeRouteAdvertisement_OrderedRanges_ReadBack()
    {
        ConnectIpRoute[] routes =
        [
            Route("10.0.0.0", "10.255.255.255", 0),
            Route("192.0.2.0", "192.0.2.0", 0),
            Route("0.0.0.0", "255.255.255.255", 17),
            Route("2001:db8::", "2001:db8::ffff", 0),
        ];

        CollectionAssert.AreEqual(routes, ConnectIpCapsuleCodec.DecodeRouteAdvertisement(ConnectIpCapsuleCodec.EncodeRouteAdvertisement(routes)).ToArray());
    }

    [TestMethod]
    [DataRow("10.0.0.9", "10.0.0.1", 0, "10.1.0.0", "10.1.0.1", 0)]
    [DataRow("10.0.0.0", "10.0.0.9", 0, "10.0.0.9", "10.0.0.20", 0)]
    [DataRow("10.0.0.0", "10.0.0.9", 0, "9.0.0.0", "9.0.0.1", 0)]
    [DataRow("10.0.0.0", "10.0.0.9", 17, "11.0.0.0", "11.0.0.1", 6)]
    [DataRow("2001:db8::", "2001:db8::1", 0, "10.0.0.0", "10.0.0.1", 0)]
    public void DecodeRouteAdvertisement_RangesBackwardsOverlappingOrOutOfOrder_AreMalformed(string start1, string end1, int protocol1, string start2, string end2, int protocol2)
    {
        var capsule = ConnectIpCapsuleCodec.EncodeRouteAdvertisement([Route(start1, end1, protocol1), Route(start2, end2, protocol2)]);

        StringAssert.Contains(
            Assert.ThrowsExactly<MasqueException>(() => ConnectIpCapsuleCodec.DecodeRouteAdvertisement(capsule)).Message,
            "malformed ROUTE_ADVERTISEMENT capsule: it holds a range that ends before it starts");
    }

    [TestMethod]
    public void DecodeRouteAdvertisement_Truncated_IsMalformed() =>
        Assert.ThrowsExactly<MasqueException>(() => ConnectIpCapsuleCodec.DecodeRouteAdvertisement(new Capsule(ConnectIpCapsuleCodec.RouteAdvertisementType, new byte[] { 4, 10, 0, 0, 0, 10, 0, 0, 1 })));

    private static ConnectIpRoute Route(string start, string end, int protocol) => new(IPAddress.Parse(start), IPAddress.Parse(end), (byte)protocol);
}
