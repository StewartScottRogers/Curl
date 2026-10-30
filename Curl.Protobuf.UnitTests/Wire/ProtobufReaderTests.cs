namespace Curl.Protobuf.Wire;

/// <summary>
/// Pins how <see cref="ProtobufReader" /> reports malformed input: a typed
/// <see cref="ProtobufDecodeError" />, never an exception.
/// </summary>
[TestClass]
public sealed class ProtobufReaderTests
{
    [TestMethod]
    public void TryReadVarint_TenBytes_DiscardsBitsPastTheSixtyFourth()
    {
        var reader = new ProtobufReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F });

        var error = reader.TryReadVarint(out var value);

        Assert.AreEqual((ProtobufDecodeError.None, ulong.MaxValue), (error, value));
    }

    [TestMethod]
    public void TryReadVarint_ContinuationBitOnLastByte_IsTruncatedVarint()
    {
        var reader = new ProtobufReader(new byte[] { 0x96 });

        Assert.AreEqual(ProtobufDecodeError.TruncatedVarint, reader.TryReadVarint(out _));
    }

    [TestMethod]
    public void TryReadVarint_ElevenBytes_IsVarintTooLong()
    {
        var reader = new ProtobufReader(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 });

        Assert.AreEqual(ProtobufDecodeError.VarintTooLong, reader.TryReadVarint(out _));
    }

    [TestMethod]
    public void TryReadFixed_TooFewBytes_IsTruncatedFixed()
    {
        Assert.AreEqual(ProtobufDecodeError.TruncatedFixed, new ProtobufReader(new byte[3]).TryReadFixed32(out _));
        Assert.AreEqual(ProtobufDecodeError.TruncatedFixed, new ProtobufReader(new byte[7]).TryReadFixed64(out _));
    }

    [TestMethod]
    public void TryReadLengthDelimited_LengthPastEnd_IsLengthPastEnd()
    {
        var reader = new ProtobufReader(new byte[] { 0x03, 0x01, 0x02 });

        Assert.AreEqual(ProtobufDecodeError.LengthPastEnd, reader.TryReadLengthDelimited(out _));
    }

    [TestMethod]
    public void TryReadLengthDelimited_TruncatedLength_IsTruncatedVarint()
    {
        var reader = new ProtobufReader(new byte[] { 0x80 });

        Assert.AreEqual(ProtobufDecodeError.TruncatedVarint, reader.TryReadLengthDelimited(out _));
    }

    [TestMethod]
    public void TryReadTag_BadTags_AreTypedErrors()
    {
        Assert.AreEqual(ProtobufDecodeError.TruncatedVarint, new ProtobufReader(ReadOnlyMemory<byte>.Empty).TryReadTag(out _, out _));
        Assert.AreEqual(ProtobufDecodeError.InvalidWireType, new ProtobufReader(new byte[] { 0x0E }).TryReadTag(out _, out _));
        Assert.AreEqual(ProtobufDecodeError.InvalidFieldNumber, new ProtobufReader(new byte[] { 0x00 }).TryReadTag(out _, out _));
        Assert.AreEqual(ProtobufDecodeError.InvalidFieldNumber, new ProtobufReader(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10 }).TryReadTag(out _, out _));
    }

    [TestMethod]
    public void TryReadField_LoneEndGroup_IsUnmatchedEndGroup()
    {
        var reader = new ProtobufReader(new byte[] { 0x0C });

        Assert.AreEqual(ProtobufDecodeError.UnmatchedEndGroup, reader.TryReadField(out var field));
        Assert.IsNull(field);
    }

    [TestMethod]
    public void TryReadField_BadTag_IsTheTagsError()
    {
        var reader = new ProtobufReader(new byte[] { 0x07 });

        Assert.AreEqual(ProtobufDecodeError.InvalidWireType, reader.TryReadField(out _));
    }

    [TestMethod]
    public void TryReadField_TruncatedValue_IsTheValuesError()
    {
        var reader = new ProtobufReader(new byte[] { 0x08, 0x80 });

        Assert.AreEqual(ProtobufDecodeError.TruncatedVarint, reader.TryReadField(out _));
    }

    [TestMethod]
    public void TryReadAllFields_MalformedField_IsItsError()
    {
        var reader = new ProtobufReader(new byte[] { 0x08, 0x01, 0x12, 0x05 });

        Assert.AreEqual(ProtobufDecodeError.LengthPastEnd, reader.TryReadAllFields(out _));
    }

    [TestMethod]
    public void TryReadField_GroupClosedByAnotherFieldNumber_IsUnmatchedEndGroup()
    {
        var reader = new ProtobufReader(new byte[] { 0x0B, 0x14 });

        Assert.AreEqual(ProtobufDecodeError.UnmatchedEndGroup, reader.TryReadField(out _));
    }

    [TestMethod]
    public void TryReadField_GroupNeverClosed_IsUnterminatedGroup()
    {
        var reader = new ProtobufReader(new byte[] { 0x0B, 0x10, 0x01 });

        Assert.AreEqual(ProtobufDecodeError.UnterminatedGroup, reader.TryReadField(out _));
    }

    [TestMethod]
    public void TryReadField_BadTagInsideGroup_IsTheTagsError()
    {
        var reader = new ProtobufReader(new byte[] { 0x0B, 0x00 });

        Assert.AreEqual(ProtobufDecodeError.InvalidFieldNumber, reader.TryReadField(out _));
    }

    [TestMethod]
    public void TryReadField_TruncatedFieldInsideGroup_IsTheFieldsError()
    {
        var reader = new ProtobufReader(new byte[] { 0x0B, 0x15, 0x01 });

        Assert.AreEqual(ProtobufDecodeError.TruncatedFixed, reader.TryReadField(out _));
    }

    [TestMethod]
    public void TryReadField_GroupsNestedOneHundredDeep_AreRead()
    {
        var reader = new ProtobufReader(NestedGroups(ProtobufReader.MaximumDepth));

        Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadField(out var field));
        Assert.IsNotNull(field);
    }

    [TestMethod]
    public void TryReadField_GroupsNestedOneHundredAndOneDeep_IsNestingTooDeep()
    {
        var reader = new ProtobufReader(NestedGroups(ProtobufReader.MaximumDepth + 1));

        Assert.AreEqual(ProtobufDecodeError.NestingTooDeep, reader.TryReadField(out _));
    }

    [TestMethod]
    public void TryReadEmbeddedMessage_MessagesNestedPastOneHundred_IsNestingTooDeep()
    {
        var payload = ReadOnlyMemory<byte>.Empty.ToArray();
        for (var level = 0; level <= ProtobufReader.MaximumDepth; level++)
        {
            var writer = new ProtobufWriter();
            writer.WriteBytesField(1, payload);
            payload = writer.ToArray();
        }

        var reader = new ProtobufReader(payload);
        var error = ProtobufDecodeError.None;
        while (error == ProtobufDecodeError.None)
        {
            Assert.AreEqual(ProtobufDecodeError.None, reader.TryReadTag(out _, out _));
            error = reader.TryReadEmbeddedMessage(out var embedded);
            reader = embedded ?? reader;
        }

        Assert.AreEqual(ProtobufDecodeError.NestingTooDeep, error);
        Assert.AreEqual(ProtobufReader.MaximumDepth, reader.Depth);
    }

    [TestMethod]
    public void TryReadEmbeddedMessage_LengthPastEnd_IsLengthPastEnd()
    {
        var reader = new ProtobufReader(new byte[] { 0x02, 0x08 });

        Assert.AreEqual(ProtobufDecodeError.LengthPastEnd, reader.TryReadEmbeddedMessage(out var embedded));
        Assert.IsNull(embedded);
    }

    [TestMethod]
    public void TryReadPacked_TruncatedElement_IsTheElementsError()
    {
        Assert.AreEqual(ProtobufDecodeError.TruncatedVarint, ProtobufReader.TryReadPackedVarints(new byte[] { 0x01, 0x80 }, new List<ulong>()));
        Assert.AreEqual(ProtobufDecodeError.TruncatedFixed, ProtobufReader.TryReadPackedFixed32(new byte[5], new List<uint>()));
        Assert.AreEqual(ProtobufDecodeError.TruncatedFixed, ProtobufReader.TryReadPackedFixed64(new byte[9], new List<ulong>()));
    }

    private static byte[] NestedGroups(int depth)
    {
        var bytes = new List<byte>();
        for (var level = 0; level < depth; level++)
        {
            bytes.Add(0x0B);
        }

        for (var level = 0; level < depth; level++)
        {
            bytes.Add(0x0C);
        }

        return [.. bytes];
    }
}
