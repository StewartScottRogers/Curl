using Curl.Tls;

namespace Curl.Extended.Protocol.Coap.Dtls.Wire;

[TestClass]
public sealed class DtlsHandshakeReassemblyTests
{
    private static readonly byte[] MessageBody = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];


    [TestMethod]
    public void Add_Rfc6347Section4_2_3_ReassemblesFragmentsArrivingOutOfOrder()
    {
        IReadOnlyList<DtlsHandshakeFragment> fragments = DtlsHandshakeFragment.Split(HandshakeType.Certificate, 3, MessageBody, 3);
        DtlsHandshakeReassembly reassembly = new(fragments[3]);
        reassembly.Add(fragments[1]);
        reassembly.Add(fragments[2]);

        Assert.IsFalse(reassembly.IsComplete);
        reassembly.Add(fragments[0]);

        Assert.IsTrue(reassembly.IsComplete);
        Assert.AreEqual(HandshakeType.Certificate, reassembly.MessageType);
        Assert.AreEqual((ushort)3, reassembly.MessageSequence);
        CollectionAssert.AreEqual(MessageBody, reassembly.ToMessageBody());
    }


    [TestMethod]
    public void Add_Rfc6347Section4_2_3_ReassemblesOverlappingAndRepeatedFragments()
    {
        DtlsHandshakeReassembly reassembly = new(Fragment(2, 6));
        reassembly.Add(Fragment(0, 4));
        reassembly.Add(Fragment(2, 6));
        reassembly.Add(Fragment(5, 5));

        CollectionAssert.AreEqual(MessageBody, reassembly.ToMessageBody());
    }


    [TestMethod]
    public void Add_Rfc6347Section4_2_3_AnEmptyMessageIsCompleteAtOnce()
    {
        DtlsHandshakeReassembly reassembly = new(DtlsHandshakeFragment.Split(HandshakeType.ServerHelloDone, 1, [], 10)[0]);

        Assert.IsTrue(reassembly.IsComplete);
        Assert.IsEmpty(reassembly.ToMessageBody());
    }


    [TestMethod]
    public void ToMessageBody_Rfc6347Section4_2_3_RefusesWhileBytesAreMissing()
    {
        DtlsHandshakeReassembly reassembly = new(Fragment(0, 4));

        Assert.ThrowsExactly<InvalidOperationException>(() => reassembly.ToMessageBody());
    }


    [TestMethod]
    [DataRow(HandshakeType.Finished, (ushort)3, 10, DisplayName = "another type")]
    [DataRow(HandshakeType.Certificate, (ushort)4, 10, DisplayName = "another message_seq")]
    [DataRow(HandshakeType.Certificate, (ushort)3, 11, DisplayName = "another length")]
    public void Add_Rfc6347Section4_2_3_RejectsAFragmentOfAnotherMessage(HandshakeType type, ushort messageSequence, int length)
    {
        DtlsHandshakeReassembly reassembly = new(Fragment(0, 4));

        Assert.ThrowsExactly<FormatException>(() => reassembly.Add(new DtlsHandshakeFragment(new DtlsHandshakeHeader(type, length, messageSequence, 4, 1), [5])));
    }


    [TestMethod]
    public void Add_Rfc6347Section4_2_3_RejectsOverlappingBytesThatDisagree()
    {
        DtlsHandshakeReassembly reassembly = new(Fragment(0, 4));

        Assert.ThrowsExactly<FormatException>(() => reassembly.Add(new DtlsHandshakeFragment(new DtlsHandshakeHeader(HandshakeType.Certificate, 10, 3, 3, 2), [99, 5])));
    }


    private static DtlsHandshakeFragment Fragment(int offset, int length) =>
        new(new DtlsHandshakeHeader(HandshakeType.Certificate, MessageBody.Length, 3, offset, length), MessageBody[offset..(offset + length)]);
}
