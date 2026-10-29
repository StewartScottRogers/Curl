namespace Curl.Protobuf.Wire;

/// <summary>
/// Pins <see cref="ProtobufWriter" /> to the byte vectors of the Protocol Buffers encoding
/// specification (protobuf.dev/programming-guides/encoding) and reads each back.
/// </summary>
[TestClass]
public sealed class ProtobufWriterTests
{
    [TestMethod]
    public void WriteInt32Field_150InField1_IsTheSpecificationsSimpleMessage()
    {
        var writer = new ProtobufWriter();
        writer.WriteInt32Field(1, 150);

        CollectionAssert.AreEqual(new byte[] { 0x08, 0x96, 0x01 }, writer.ToArray());
        var reader = new ProtobufReader(writer.ToArray());
        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadTag(out var fieldNumber, out var wireType));
        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadVarint(out var value));
        Assert.AreEqual((1, ProtobufWireType.Varint, 150UL), (fieldNumber, wireType, value));
        Assert.IsTrue(reader.IsAtEnd);
    }

    [TestMethod]
    public void WriteInt32Field_MinusOne_TakesTenBytes()
    {
        var writer = new ProtobufWriter();
        writer.WriteInt32Field(1, -1);

        CollectionAssert.AreEqual(new byte[] { 0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 }, writer.ToArray());
        var reader = new ProtobufReader(writer.ToArray().AsMemory(1));
        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadVarint(out var value));
        Assert.AreEqual(-1, (int)value);
    }

    [TestMethod]
    public void WriteInt64Field_MinusTwo_TakesTenBytes()
    {
        var writer = new ProtobufWriter();
        writer.WriteInt64Field(1, -2);

        CollectionAssert.AreEqual(new byte[] { 0x08, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 }, writer.ToArray());
    }

    [TestMethod]
    public void WriteSInt32Field_MinusTwo_IsZigZagThree()
    {
        var writer = new ProtobufWriter();
        writer.WriteSInt32Field(1, -2);

        CollectionAssert.AreEqual(new byte[] { 0x08, 0x03 }, writer.ToArray());
    }

    [TestMethod]
    public void WriteSInt64Field_MinusOne_IsZigZagOne()
    {
        var writer = new ProtobufWriter();
        writer.WriteSInt64Field(1, -1);

        CollectionAssert.AreEqual(new byte[] { 0x08, 0x01 }, writer.ToArray());
    }

    [TestMethod]
    public void WriteStringField_TestingInField2_IsTheSpecificationsStringVector()
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(2, "testing");

        CollectionAssert.AreEqual(new byte[] { 0x12, 0x07, 0x74, 0x65, 0x73, 0x74, 0x69, 0x6E, 0x67 }, writer.ToArray());
        var reader = new ProtobufReader(writer.ToArray());
        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadTag(out _, out _));
        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadLengthDelimited(out var payload));
        Assert.AreEqual("testing", System.Text.Encoding.UTF8.GetString(payload.Span));
    }

    [TestMethod]
    public void WriteBytesField_EmbeddedMessageInField3_IsTheSpecificationsEmbeddedVector()
    {
        var inner = new ProtobufWriter();
        inner.WriteInt32Field(1, 150);
        var writer = new ProtobufWriter();
        writer.WriteBytesField(3, inner.WrittenBytes);

        CollectionAssert.AreEqual(new byte[] { 0x1A, 0x03, 0x08, 0x96, 0x01 }, writer.ToArray());
        var reader = new ProtobufReader(writer.ToArray());
        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadTag(out _, out _));
        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadEmbeddedMessage(out var embedded));
        Assert.AreEqual(1, embedded!.Depth);
        Assert.AreEqual(ProtobufDecodeError.None, embedded.TryReadAllFields(out var fields));
        Assert.AreEqual(150UL, fields.Single().Scalar);
    }

    [TestMethod]
    public void WritePackedVarintsField_TheSpecificationsPackedVector_RoundTrips()
    {
        var writer = new ProtobufWriter();
        writer.WritePackedVarintsField(4, [3, 270, 86942]);

        CollectionAssert.AreEqual(new byte[] { 0x22, 0x06, 0x03, 0x8E, 0x02, 0x9E, 0xA7, 0x05 }, writer.ToArray());
        var values = new List<ulong>();
        Assert.AreEqual(ProtobufDecodeError.None, ProtobufReader.TryReadPackedVarints(writer.ToArray().AsMemory(2), values));
        CollectionAssert.AreEqual(new ulong[] { 3, 270, 86942 }, values);
    }

    [TestMethod]
    public void WriteFixed32Field_OneInField2_IsTagThenFourLittleEndianBytes()
    {
        var writer = new ProtobufWriter();
        writer.WriteFixed32Field(2, 1);

        CollectionAssert.AreEqual(new byte[] { 0x15, 0x01, 0x00, 0x00, 0x00 }, writer.ToArray());
        var reader = new ProtobufReader(writer.ToArray().AsMemory(1));
        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadFixed32(out var value));
        Assert.AreEqual(1U, value);
    }

    [TestMethod]
    public void WriteFixed64Field_InField1_IsTagThenEightLittleEndianBytes()
    {
        var writer = new ProtobufWriter();
        writer.WriteFixed64Field(1, 0x0102030405060708);

        CollectionAssert.AreEqual(new byte[] { 0x09, 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 }, writer.ToArray());
        var reader = new ProtobufReader(writer.ToArray().AsMemory(1));
        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadFixed64(out var value));
        Assert.AreEqual(0x0102030405060708UL, value);
    }

    [TestMethod]
    public void WritePackedFixed32Field_TwoValues_RoundTrips()
    {
        var writer = new ProtobufWriter();
        writer.WritePackedFixed32Field(5, [1, 0xFFFFFFFF]);

        CollectionAssert.AreEqual(new byte[] { 0x2A, 0x08, 0x01, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF }, writer.ToArray());
        var values = new List<uint>();
        Assert.AreEqual(ProtobufDecodeError.None, ProtobufReader.TryReadPackedFixed32(writer.ToArray().AsMemory(2), values));
        CollectionAssert.AreEqual(new uint[] { 1, 0xFFFFFFFF }, values);
    }

    [TestMethod]
    public void WritePackedFixed64Field_OneValue_RoundTrips()
    {
        var writer = new ProtobufWriter();
        writer.WritePackedFixed64Field(6, [2]);

        CollectionAssert.AreEqual(new byte[] { 0x32, 0x08, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, writer.ToArray());
        var values = new List<ulong>();
        Assert.AreEqual(ProtobufDecodeError.None, ProtobufReader.TryReadPackedFixed64(writer.ToArray().AsMemory(2), values));
        CollectionAssert.AreEqual(new ulong[] { 2 }, values);
    }

    [TestMethod]
    public void WriteTag_LargestFieldNumber_IsFiveByteVarint()
    {
        var writer = new ProtobufWriter();
        writer.WriteTag(ProtobufReader.MaximumFieldNumber, ProtobufWireType.Varint);

        CollectionAssert.AreEqual(new byte[] { 0xF8, 0xFF, 0xFF, 0xFF, 0x0F }, writer.ToArray());
    }

    [TestMethod]
    public void WriteTag_FieldNumberOutOfRange_Throws()
    {
        var writer = new ProtobufWriter();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => writer.WriteTag(0, ProtobufWireType.Varint));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => writer.WriteTag(ProtobufReader.MaximumFieldNumber + 1, ProtobufWireType.Varint));
    }

    [TestMethod]
    public void WriteField_EndGroupOrUndefinedWireType_Throws()
    {
        var writer = new ProtobufWriter();

        Assert.ThrowsExactly<ArgumentException>(() => writer.WriteField(new ProtobufField(1, ProtobufWireType.EndGroup, 0, ReadOnlyMemory<byte>.Empty, [])));
        Assert.ThrowsExactly<ArgumentException>(() => writer.WriteField(new ProtobufField(1, (ProtobufWireType)6, 0, ReadOnlyMemory<byte>.Empty, [])));
    }

    [TestMethod]
    public void UnknownFields_EveryWireTypeAndANestedGroup_RoundTripByteForByte()
    {
        byte[] message =
        [
            0x08, 0x96, 0x01,
            0x11, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x1A, 0x02, 0x68, 0x69,
            0x23, 0x28, 0x01, 0x33, 0x3D, 0x01, 0x00, 0x00, 0x00, 0x34, 0x24,
            0x45, 0xFF, 0xFF, 0xFF, 0xFF,
        ];
        var reader = new ProtobufReader(message);

        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadAllFields(out var fields));
        var writer = new ProtobufWriter();
        foreach (var field in fields)
        {
            writer.WriteField(field);
        }

        CollectionAssert.AreEqual(message, writer.ToArray());
        Assert.AreEqual(5, fields.Count);
        Assert.AreEqual(ProtobufWireType.StartGroup, fields[3].WireType);
        Assert.AreEqual(ProtobufWireType.StartGroup, fields[3].GroupFields[1].WireType);
        Assert.AreEqual(1UL, fields[3].GroupFields[1].GroupFields[0].Scalar);
    }
}
