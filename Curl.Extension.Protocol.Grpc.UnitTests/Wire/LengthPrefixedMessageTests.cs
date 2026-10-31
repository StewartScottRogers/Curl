namespace Curl.Extension.Protocol.Grpc.Wire;

/// <summary>Checks the Length-Prefixed-Message of PROTOCOL-HTTP2.md, framed and read back.</summary>
[TestClass]
public sealed class LengthPrefixedMessageTests
{
    [TestMethod]
    public void Frame_LengthPrefixedMessage_PutsTheFlagAndBigEndianLengthFirst()
    {
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 2, 0xAA, 0xBB }, LengthPrefixedMessage.Frame([0xAA, 0xBB], false));
        CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 0, 0 }, LengthPrefixedMessage.Frame([], true));
    }

    [TestMethod]
    public void TryRead_LengthPrefixedMessage_SeveralInOneRead_GivesEachInTurn()
    {
        var reader = new LengthPrefixedMessageReader();
        reader.Append([.. LengthPrefixedMessage.Frame([1, 2, 3], false), .. LengthPrefixedMessage.Frame([4], true)]);

        Assert.IsTrue(reader.TryRead(out var first));
        Assert.IsFalse(first.IsCompressed);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, first.Payload);
        Assert.IsTrue(reader.TryRead(out var second));
        Assert.IsTrue(second.IsCompressed);
        CollectionAssert.AreEqual(new byte[] { 4 }, second.Payload);
        Assert.IsFalse(reader.TryRead(out var none));
        Assert.IsNull(none);
        reader.Complete();
    }

    [TestMethod]
    public void TryRead_LengthPrefixedMessage_SplitAcrossReads_WaitsForTheWholeMessage()
    {
        var payload = Enumerable.Range(0, 1000).Select(value => (byte)value).ToArray();
        var framed = LengthPrefixedMessage.Frame(payload, false);
        var reader = new LengthPrefixedMessageReader();
        GrpcMessage? message = null;
        for (var offset = 0; offset < framed.Length; offset += 7)
        {
            Assert.IsNull(message);
            reader.Append(framed.AsSpan(offset, Math.Min(7, framed.Length - offset)));
            reader.TryRead(out message);
        }

        Assert.IsNotNull(message);
        CollectionAssert.AreEqual(payload, message.Payload);
        reader.Complete();
    }

    [TestMethod]
    public void Append_LengthPrefixedMessage_AfterReading_ReusesTheBuffer()
    {
        var reader = new LengthPrefixedMessageReader();
        var framed = LengthPrefixedMessage.Frame(new byte[200], false);
        reader.Append(framed);
        Assert.IsTrue(reader.TryRead(out _));
        reader.Append(framed.AsSpan(0, 100));
        reader.Append(framed.AsSpan(100));

        Assert.IsTrue(reader.TryRead(out var message));
        Assert.HasCount(200, message.Payload);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 0, 0 })]
    [DataRow(new byte[] { 0, 0, 0, 0, 4, 1, 2 })]
    public void Complete_LengthPrefixedMessage_Truncated_FailsWithAWireError(byte[] body)
    {
        var reader = new LengthPrefixedMessageReader();
        reader.Append(body);

        Assert.IsFalse(reader.TryRead(out _));
        var error = Assert.ThrowsExactly<GrpcWireException>(reader.Complete);
        StringAssert.Contains(error.Message, $"{body.Length} bytes into it");
    }

    [TestMethod]
    public void TryRead_LengthPrefixedMessage_UndefinedCompressedFlag_FailsWithAWireError()
    {
        var reader = new LengthPrefixedMessageReader();
        reader.Append([2, 0, 0, 0, 0]);

        Assert.ThrowsExactly<GrpcWireException>(() => reader.TryRead(out _));
    }

    [TestMethod]
    public void TryRead_LengthPrefixedMessage_LongerThanTheMaximum_FailsWithAWireError()
    {
        var reader = new LengthPrefixedMessageReader(maximumMessageLength: 3);
        reader.Append([0, 0, 0, 0, 3, 1, 2, 3]);
        Assert.IsTrue(reader.TryRead(out _));
        reader.Append([0, 0xFF, 0xFF, 0xFF, 0xFF]);

        Assert.ThrowsExactly<GrpcWireException>(() => reader.TryRead(out _));
    }

    [TestMethod]
    public void Constructor_LengthPrefixedMessage_NegativeMaximum_IsRefused() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LengthPrefixedMessageReader(-1));
}
