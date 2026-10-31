using Curl.Extended.Protocol.DnsQuery.Wire;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;

[TestClass]
public sealed class DnsRecordDataDecoderTests
{
    [TestMethod]
    [DataRow(1, new byte[] { 93, 184, 216, 34, 0 })]
    [DataRow(1, new byte[] { 93, 184, 216 })]
    [DataRow(28, new byte[] { 0x20, 0x01 })]
    [DataRow(15, new byte[] { 0, 10, 0, 0 })]
    [DataRow(16, new byte[] { 5, (byte)'a' })]
    [DataRow(33, new byte[] { 0, 1, 0, 2, 0 })]
    public void Decode_Rfc1035Section413RdataNotFittingRdlength_Fails(int type, byte[] data)
    {
        DnsOutcome<DnsMessage> outcome = DnsMessageDecoder.Decode(DnsWire.SingleAnswer(type, data));

        Assert.IsNull(outcome.Value);
        Assert.IsNotNull(outcome.Failure);
    }

    [TestMethod]
    public void Decode_Rfc1035Section413RdlengthPastTheMessage_Fails()
    {
        byte[] message = DnsWire.SingleAnswer(65, [1, 2]);
        message[^3] = 9;

        Assert.AreEqual("The DNS message ends inside a field.", DnsMessageDecoder.Decode(message).Failure);
    }

    [TestMethod]
    public void Decode_Rfc8659CaaTagPastRdlength_Fails()
    {
        byte[] data = [0, 5, (byte)'i', (byte)'s', (byte)'s', (byte)'u', (byte)'e'];
        byte[] message = DnsWire.Join(DnsWire.SingleAnswer(257, data[..3]), data[3..]);

        Assert.AreEqual("A CAA record's tag runs past its RDLENGTH.", DnsMessageDecoder.Decode(message).Failure);
    }

    [TestMethod]
    public void Decode_Rfc1035Section3314TxtWithNoString_GivesAnEmptyField()
    {
        DnsRecordField field = DnsMessageDecoder.Decode(DnsWire.SingleAnswer(16, [])).Value!.Answers.Single().Data.Single();

        Assert.AreEqual(DnsFieldKind.CharacterStrings, field.Kind);
        Assert.IsEmpty(field.Values);
    }
}
