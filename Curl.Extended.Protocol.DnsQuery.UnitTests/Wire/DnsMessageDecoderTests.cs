using Curl.Extended.Protocol.DnsQuery.Wire;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;

[TestClass]
public sealed class DnsMessageDecoderTests
{
    [TestMethod]
    public void Decode_Rfc1035Section41AllSections_ReadsHeaderQuestionAnswerAuthorityAndAdditional()
    {
        byte[] message = new DnsWire()
            .Header(0x1234, 0x8583, 1, 1, 1, 1)
            .Question(DnsWire.Name("www.example.com"), 1, 1)
            .Record(DnsWire.Pointer(DnsWire.FirstQuestionName), 5, 1, 60, DnsWire.Join(DnsWire.Name("cdn"), []))
            .Record(DnsWire.Join([3, (byte)'s', (byte)'u', (byte)'b'], DnsWire.Pointer(16)), 2, 1, 120, DnsWire.Pointer(16))
            .Record(DnsWire.Name(""), 41, 1232, 0, [])
            .ToArray();

        DnsMessage decoded = DnsMessageDecoder.Decode(message).Value!;

        Assert.AreEqual(new DnsHeader(0x1234, 0x8583), decoded.Header);
        Assert.AreEqual(new DnsQuestion("www.example.com.", 1, 1), decoded.Questions.Single());
        DnsResourceRecord answer = decoded.Answers.Single();
        Assert.AreEqual("www.example.com.", answer.Name);
        Assert.AreEqual((ushort)5, answer.Type);
        Assert.AreEqual(60u, answer.TimeToLive);
        Assert.AreEqual("cdn.", answer.Data.Single().Values.Single());
        DnsResourceRecord authority = decoded.Authority.Single();
        Assert.AreEqual("sub.example.com.", authority.Name);
        Assert.AreEqual("example.com.", authority.Data.Single().Values.Single());
        DnsResourceRecord additional = decoded.Additional.Single();
        Assert.AreEqual(".", additional.Name);
        Assert.AreEqual((ushort)1232, additional.Class);
        Assert.AreEqual(@"\# 0", additional.Data.Single().Values.Single());
    }

    [TestMethod]
    public void Decode_Rfc1035Section411Header_ExposesFlagsOpcodeAndResponseCode()
    {
        DnsHeader header = DnsMessageDecoder.Decode(new DnsWire().Header(7, 0xAFB3, 0, 0, 0, 0).ToArray()).Value!.Header;

        Assert.AreEqual(5, header.Opcode);
        Assert.AreEqual(3, header.ResponseCode);
        CollectionAssert.AreEqual(new[] { "qr", "aa", "tc", "rd", "ra", "ad", "cd" }, header.SetFlagNames().ToArray());
        Assert.IsEmpty(new DnsHeader(7, 0).SetFlagNames());
    }

    [TestMethod]
    [DataRow(new byte[] { 0x12, 0x34, 0x81 })]
    [DataRow(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 3, (byte)'c', (byte)'o' })]
    [DataRow(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1 })]
    [DataRow(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0xC0 })]
    public void Decode_Rfc1035Section41TruncatedMessage_FailsWithoutThrowing(byte[] message)
    {
        DnsOutcome<DnsMessage> outcome = DnsMessageDecoder.Decode(message);

        Assert.IsNull(outcome.Value);
        Assert.AreEqual("The DNS message ends inside a field.", outcome.Failure);
    }

    [TestMethod]
    public void Decode_Rfc1035Section41CountsPastTheEnd_FailsRatherThanHangs()
    {
        byte[] message = new DnsWire().Header(0, 0, 0, 0xFFFF, 0xFFFF, 0xFFFF).ToArray();

        Assert.AreEqual("The DNS message ends inside a field.", DnsMessageDecoder.Decode(message).Failure);
    }

    [TestMethod]
    public void Decode_Rfc1035Section414PointerToANameEndingInAPointer_FollowsBothAndResumesAfterTheFirst()
    {
        byte[] message = new DnsWire()
            .Header(0, 0, 3, 0, 0, 0)
            .Question(DnsWire.Name("example.com"), 1, 1)
            .Question(DnsWire.Join([3, (byte)'w', (byte)'w', (byte)'w'], DnsWire.Pointer(DnsWire.FirstQuestionName)), 28, 1)
            .Question(DnsWire.Pointer(29), 16, 3)
            .ToArray();

        DnsMessage decoded = DnsMessageDecoder.Decode(message).Value!;

        Assert.AreEqual(new DnsQuestion("www.example.com.", 16, 3), decoded.Questions[2]);
    }

    [TestMethod]
    public void Decode_Rfc1035Section414PointerToItself_FailsAsALoop()
    {
        byte[] message = new DnsWire().Header(0, 0, 1, 0, 0, 0).Bytes(DnsWire.Pointer(DnsWire.FirstQuestionName)).Bytes([0, 1, 0, 1]).ToArray();

        Assert.AreEqual("A DNS compression pointer does not point back to an earlier name.", DnsMessageDecoder.Decode(message).Failure);
    }

    [TestMethod]
    public void Decode_Rfc1035Section414PointerForwardAsEveryLoopNeeds_Fails()
    {
        byte[] message = new DnsWire()
            .Header(0, 0, 2, 0, 0, 0)
            .Question(DnsWire.Join([1, (byte)'a'], DnsWire.Pointer(17)), 1, 1)
            .Question(DnsWire.Pointer(DnsWire.FirstQuestionName), 1, 1)
            .ToArray();

        Assert.AreEqual("A DNS compression pointer does not point back to an earlier name.", DnsMessageDecoder.Decode(message).Failure);
    }

    [TestMethod]
    [DataRow((byte)0x40)]
    [DataRow((byte)0x80)]
    public void Decode_Rfc1035Section414LabelTypeOtherThanLengthOrPointer_Fails(byte labelType)
    {
        byte[] message = new DnsWire().Header(0, 0, 1, 0, 0, 0).Bytes([labelType, 0, 0, 1, 0, 1]).ToArray();

        Assert.AreEqual("A DNS name has a label type RFC 1035 does not define.", DnsMessageDecoder.Decode(message).Failure);
    }

    [TestMethod]
    public void Decode_Rfc1035Section232NameOf255Octets_IsReadAndOneMoreFails()
    {
        string label = new('a', 63);
        string longest = $"{label}.{label}.{label}.{new string('a', 61)}";
        byte[] fits = new DnsWire().Header(0, 0, 1, 0, 0, 0).Question(DnsWire.Name(longest), 1, 1).ToArray();
        byte[] tooLong = new DnsWire().Header(0, 0, 1, 0, 0, 0).Question(DnsWire.Name(longest + "a"), 1, 1).ToArray();

        Assert.AreEqual(longest + ".", DnsMessageDecoder.Decode(fits).Value!.Questions.Single().Name);
        Assert.AreEqual("A DNS name is longer than 255 octets.", DnsMessageDecoder.Decode(tooLong).Failure);
    }

    [TestMethod]
    public void Decode_Rfc1035Section414PointedToLabelsCountTowardTheLength_FailsOver255Octets()
    {
        string label = new('a', 63);
        byte[] message = new DnsWire()
            .Header(0, 0, 2, 0, 0, 0)
            .Question(DnsWire.Name($"{label}.{label}.{label}"), 1, 1)
            .Question(DnsWire.Join([63, .. new byte[63]], DnsWire.Pointer(DnsWire.FirstQuestionName)), 1, 1)
            .ToArray();

        Assert.AreEqual("A DNS name is longer than 255 octets.", DnsMessageDecoder.Decode(message).Failure);
    }
}
