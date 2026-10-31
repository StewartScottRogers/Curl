using Curl.Extended.Protocol.Coap.Dtls.TestServer;
using Curl.Extended.Protocol.Coap.Dtls.Wire;
using TlsContentType = Curl.Tls.TlsContentType;

namespace Curl.Extended.Protocol.Coap.Dtls.Handshake;

[TestClass]
public sealed class DtlsClientSessionTests
{
    [TestMethod]
    public async Task SendAsyncThenReceiveAsync_Rfc5288_ApplicationDataRoundTripsProtectedWithAes128Gcm()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server);
        using DtlsClientSession session = await DtlsClientHandshakeTests.RunAsync(channel, insecure: true);

        await session.SendAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
        byte[] echoed = await session.ReceiveAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, server.ReceivedApplicationData.Single());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, echoed);
        DtlsRecordHeader sent = DtlsRecordHeader.Decode(channel.Sent.Last());
        Assert.AreEqual(TlsContentType.ApplicationData, sent.ContentType);
        Assert.AreEqual((ushort)1, sent.Epoch);
        Assert.AreEqual(8 + 3 + 16, sent.Length);
    }

    [TestMethod]
    public async Task SendAsyncThenReceiveAsync_Rfc7251_ApplicationDataRoundTripsProtectedWithAes128Ccm8()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa) { UseAes128Ccm8 = true };
        DtlsTestServerChannel channel = new(server);
        using DtlsClientSession session = await DtlsClientHandshakeTests.RunAsync(channel, insecure: true);

        await session.SendAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
        byte[] echoed = await session.ReceiveAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, server.ReceivedApplicationData.Single());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, echoed);
        Assert.AreEqual(8 + 3 + 8, DtlsRecordHeader.Decode(channel.Sent.Last()).Length);
    }

    [TestMethod]
    public async Task ReceiveAsync_Rfc6347Section4_1_2_6_ReplayedRecord_IsDropped()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server);
        using DtlsClientSession session = await DtlsClientHandshakeTests.RunAsync(channel, insecure: true);
        await session.SendAsync(new byte[] { 1 }, CancellationToken.None);
        await session.ReceiveAsync(CancellationToken.None);
        channel.Deliver(channel.Delivered.Last());

        await session.SendAsync(new byte[] { 2 }, CancellationToken.None);
        byte[] received = await session.ReceiveAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 2 }, received);
    }

    [TestMethod]
    public async Task ReceiveAsync_Rfc6347Section4_1_UnprotectedRecords_AreDropped()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server);
        using DtlsClientSession session = await DtlsClientHandshakeTests.RunAsync(channel, insecure: true);
        channel.Deliver(Epoch0Record(TlsContentType.ApplicationData, [9, 9]));
        channel.Deliver(Epoch0Record(TlsContentType.ChangeCipherSpec, [1]));

        await session.SendAsync(new byte[] { 7 }, CancellationToken.None);
        byte[] received = await session.ReceiveAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 7 }, received);
    }

    private static byte[] Epoch0Record(TlsContentType contentType, byte[] fragment) =>
        [.. new DtlsRecordHeader(contentType, DtlsRecordHeader.Dtls12Version, 0, 99, (ushort)fragment.Length).Encode(), .. fragment];
}
