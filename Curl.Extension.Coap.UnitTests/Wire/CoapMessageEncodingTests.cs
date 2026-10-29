namespace Curl.Extension.Coap.Wire;

[TestClass]
public sealed class CoapMessageEncodingTests
{
    [TestMethod]
    public void Encode_Rfc7252Section3_WritesHeaderTokenOptionsMarkerAndPayload()
    {
        CoapMessage message = new(
            CoapMessageType.Confirmable,
            CoapCode.Get,
            0x7D34,
            [0xAB, 0xCD],
            [new CoapOption(CoapOptionNumber.UriPath, "temp"u8.ToArray())],
            [0x01, 0x02]);

        byte[] bytes = CoapMessageEncoding.Encode(message);

        CollectionAssert.AreEqual(
            new byte[] { 0x42, 0x01, 0x7D, 0x34, 0xAB, 0xCD, 0xB4, (byte)'t', (byte)'e', (byte)'m', (byte)'p', 0xFF, 0x01, 0x02 },
            bytes);
    }

    [TestMethod]
    public void Encode_Rfc7252Section3_WritesNoPayloadMarkerWithoutAPayload()
    {
        CoapMessage message = new(CoapMessageType.Acknowledgement, CoapCode.Empty, 1, [], [], []);

        CollectionAssert.AreEqual(new byte[] { 0x60, 0x00, 0x00, 0x01 }, CoapMessageEncoding.Encode(message));
    }

    [TestMethod]
    public void Encode_Rfc7252Section3_1_WritesOptionsInAscendingNumberAsDeltas()
    {
        CoapMessage message = new(
            CoapMessageType.NonConfirmable,
            CoapCode.Post,
            2,
            [],
            [new CoapOption(CoapOptionNumber.UriPath, [0x62]), new CoapOption(CoapOptionNumber.UriHost, [0x61]), new CoapOption(CoapOptionNumber.UriPath, [0x63])],
            []);

        CollectionAssert.AreEqual(
            new byte[] { 0x50, 0x02, 0x00, 0x02, 0x31, 0x61, 0x81, 0x62, 0x01, 0x63 },
            CoapMessageEncoding.Encode(message));
    }

    [TestMethod]
    [DataRow(13, 0, new byte[] { 0xD0, 0x00 })]
    [DataRow(268, 0, new byte[] { 0xD0, 0xFF })]
    [DataRow(269, 0, new byte[] { 0xE0, 0x00, 0x00 })]
    [DataRow(65535, 0, new byte[] { 0xE0, 0xFE, 0xF2 })]
    [DataRow(0, 13, new byte[] { 0x0D, 0x00 })]
    [DataRow(0, 269, new byte[] { 0x0E, 0x00, 0x00 })]
    public void Encode_Rfc7252Section3_1_WritesExtendedDeltaAndLength(int number, int length, byte[] optionHeader)
    {
        CoapMessage message = new(CoapMessageType.Confirmable, CoapCode.Get, 0, [], [new CoapOption(number, new byte[length])], []);

        byte[] bytes = CoapMessageEncoding.Encode(message);

        CollectionAssert.AreEqual(optionHeader, bytes[4..(4 + optionHeader.Length)]);
        Assert.HasCount(4 + optionHeader.Length + length, bytes);
    }

    [TestMethod]
    public void Encode_Rfc7252Section3_RejectsATokenLongerThan8Bytes()
    {
        CoapMessage message = new(CoapMessageType.Confirmable, CoapCode.Get, 0, new byte[9], [], []);

        Assert.ThrowsExactly<ArgumentException>(() => CoapMessageEncoding.Encode(message));
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(65536, 0)]
    [DataRow(1, CoapMessageEncoding.MaximumExtendedValue + 1)]
    public void Encode_Rfc7252Section3_1_RejectsAnOptionOutOfRange(int number, int length)
    {
        CoapMessage message = new(CoapMessageType.Confirmable, CoapCode.Get, 0, [], [new CoapOption(number, new byte[length])], []);

        Assert.ThrowsExactly<ArgumentException>(() => CoapMessageEncoding.Encode(message));
    }

    [TestMethod]
    public void Encode_Rfc7252Section3_RejectsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CoapMessageEncoding.Encode(null!));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(8)]
    public void Decode_Rfc7252Section3_RoundTripsEveryTokenLengthAndType(int tokenLength)
    {
        foreach (CoapMessageType type in Enum.GetValues<CoapMessageType>())
        {
            byte[] token = Enumerable.Range(1, tokenLength).Select(b => (byte)b).ToArray();
            CoapMessage message = new(type, CoapCode.Content, 0xFFFE, token, [], [0x2A]);

            CoapMessage decoded = CoapMessageEncoding.Decode(CoapMessageEncoding.Encode(message));

            Assert.AreEqual(type, decoded.Type);
            Assert.AreEqual(CoapCode.Content, decoded.Code);
            Assert.AreEqual((ushort)0xFFFE, decoded.MessageId);
            CollectionAssert.AreEqual(token, decoded.Token);
            CollectionAssert.AreEqual(new byte[] { 0x2A }, decoded.Payload);
        }
    }

    [TestMethod]
    public void Decode_Rfc7252Section3_1_RoundTripsOptionsAcrossEveryDeltaAndLengthForm()
    {
        CoapOption[] options =
        [
            new(CoapOptionNumber.IfMatch, []),
            new(CoapOptionNumber.UriPath, new byte[12]),
            new(CoapOptionNumber.UriPath, new byte[13]),
            new(CoapOptionNumber.Block2, new byte[268]),
            new(CoapOptionNumber.Size1, new byte[269]),
            new(300, new byte[1000]),
            new(65535, [0x07]),
        ];
        CoapMessage message = new(CoapMessageType.Confirmable, CoapCode.Put, 9, [0x01], options, "hi"u8.ToArray());

        CoapMessage decoded = CoapMessageEncoding.Decode(CoapMessageEncoding.Encode(message));

        Assert.HasCount(options.Length, decoded.Options);
        for (int i = 0; i < options.Length; i++)
        {
            Assert.AreEqual(options[i].Number, decoded.Options[i].Number);
            CollectionAssert.AreEqual(options[i].Value, decoded.Options[i].Value);
        }

        CollectionAssert.AreEqual("hi"u8.ToArray(), decoded.Payload);
    }

    [TestMethod]
    public void Decode_Rfc7252Section3_ReadsAMessageWithNoOptionsOrPayload()
    {
        CoapMessage decoded = CoapMessageEncoding.Decode([0x41, 0x01, 0x00, 0x05, 0x99]);

        CollectionAssert.AreEqual(new byte[] { 0x99 }, decoded.Token);
        Assert.IsEmpty(decoded.Options);
        Assert.IsEmpty(decoded.Payload);
    }

    [TestMethod]
    public void Decode_Rfc7252Section4_1_ReadsAnEmptyMessage()
    {
        CoapMessage decoded = CoapMessageEncoding.Decode([0x70, 0x00, 0x12, 0x34]);

        Assert.AreEqual(CoapMessageType.Reset, decoded.Type);
        Assert.AreEqual(CoapCode.Empty, decoded.Code);
        Assert.AreEqual((ushort)0x1234, decoded.MessageId);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x40, 0x01, 0x00 }, DisplayName = "shorter than the header")]
    [DataRow(new byte[] { 0x80, 0x01, 0x00, 0x00 }, DisplayName = "version 2")]
    [DataRow(new byte[] { 0x00, 0x01, 0x00, 0x00 }, DisplayName = "version 0")]
    [DataRow(new byte[] { 0x49, 0x01, 0x00, 0x00, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, DisplayName = "token length 9")]
    [DataRow(new byte[] { 0x4F, 0x01, 0x00, 0x00 }, DisplayName = "token length 15")]
    [DataRow(new byte[] { 0x40, 0x00, 0x00, 0x00, 0xFF, 0x01 }, DisplayName = "Empty message with a payload")]
    [DataRow(new byte[] { 0x41, 0x00, 0x00, 0x00, 0x01 }, DisplayName = "Empty message with a token")]
    [DataRow(new byte[] { 0x42, 0x01, 0x00, 0x00, 0x01 }, DisplayName = "token past the end")]
    [DataRow(new byte[] { 0x40, 0x01, 0x00, 0x00, 0xF0 }, DisplayName = "delta nibble 15")]
    [DataRow(new byte[] { 0x40, 0x01, 0x00, 0x00, 0x1F }, DisplayName = "length nibble 15")]
    [DataRow(new byte[] { 0x40, 0x01, 0x00, 0x00, 0xFF }, DisplayName = "payload marker with no payload")]
    [DataRow(new byte[] { 0x40, 0x01, 0x00, 0x00, 0x13, 0x61 }, DisplayName = "option value past the end")]
    [DataRow(new byte[] { 0x40, 0x01, 0x00, 0x00, 0xD0 }, DisplayName = "one-byte delta extension past the end")]
    [DataRow(new byte[] { 0x40, 0x01, 0x00, 0x00, 0x0E, 0x00 }, DisplayName = "two-byte length extension past the end")]
    public void Decode_Rfc7252Section3_RejectsAMessageFormatError(byte[] datagram)
    {
        Assert.ThrowsExactly<FormatException>(() => CoapMessageEncoding.Decode(datagram));
    }
}
