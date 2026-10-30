using Curl.Extension.Coap.Dtls.Wire;
using Curl.Tls;

namespace Curl.Extension.Coap.Dtls.Handshake;

[TestClass]
public sealed class DtlsHandshakeMessageReaderTests
{
    [TestMethod]
    public void TryTakeNext_Rfc6347Section4_2_2_MessageArrivingEarly_IsHeldUntilTheOneBeforeIt()
    {
        DtlsHandshakeMessageReader reader = new();
        reader.Add(Whole(HandshakeType.Certificate, 1, [7]));

        bool tookEarly = reader.TryTakeNext(out _);
        reader.Add(Whole(HandshakeType.ServerHello, 0, [6]));
        bool tookFirst = reader.TryTakeNext(out DtlsHandshakeMessage first);
        bool tookSecond = reader.TryTakeNext(out DtlsHandshakeMessage second);

        Assert.IsFalse(tookEarly);
        Assert.IsTrue(tookFirst);
        Assert.IsTrue(tookSecond);
        Assert.AreEqual(HandshakeType.ServerHello, first.MessageType);
        Assert.AreEqual((ushort)1, second.MessageSequence);
        CollectionAssert.AreEqual(new byte[] { 7 }, second.Body);
    }

    [TestMethod]
    public void TryTakeNext_Rfc6347Section4_2_3_Fragments_AreGivenOnceTheMessageIsWhole()
    {
        DtlsHandshakeMessageReader reader = new();
        reader.Add(new DtlsHandshakeFragment(new DtlsHandshakeHeader(HandshakeType.ServerHello, 4, 0, 0, 2), [1, 2]));

        bool tookHalf = reader.TryTakeNext(out _);
        reader.Add(new DtlsHandshakeFragment(new DtlsHandshakeHeader(HandshakeType.ServerHello, 4, 0, 2, 2), [3, 4]));
        bool tookWhole = reader.TryTakeNext(out DtlsHandshakeMessage message);

        Assert.IsFalse(tookHalf);
        Assert.IsTrue(tookWhole);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, message.Body);
    }

    [TestMethod]
    public void Add_Rfc6347Section4_2_4_RetransmittedMessageAlreadyGiven_IsDropped()
    {
        DtlsHandshakeMessageReader reader = new();
        reader.Add(Whole(HandshakeType.ServerHello, 0, [1]));
        reader.TryTakeNext(out _);

        reader.Add(Whole(HandshakeType.ServerHello, 0, [1]));

        Assert.IsFalse(reader.TryTakeNext(out _));
    }

    [TestMethod]
    public void TranscriptBytes_Rfc6347Section4_2_6_IsTheHeaderOfOneWholeFragmentThenTheBody()
    {
        DtlsHandshakeMessage message = new(HandshakeType.Finished, 5, [0xAA, 0xBB]);

        byte[] bytes = message.TranscriptBytes();

        CollectionAssert.AreEqual(new byte[] { 20, 0, 0, 2, 0, 5, 0, 0, 0, 0, 0, 2, 0xAA, 0xBB }, bytes);
    }

    private static DtlsHandshakeFragment Whole(HandshakeType type, ushort messageSequence, byte[] body) =>
        new(new DtlsHandshakeHeader(type, body.Length, messageSequence, 0, body.Length), body);
}
