namespace Curl.Tls;

/// <summary>
/// Checks the presentation-language primitives under the codecs: fixed-width integers,
/// vectors and their length limits, the sticky failure a <see cref="TlsReader" /> shares
/// with its vectors, and <see cref="TlsDecodeResult{T}" />.
/// </summary>
[TestClass]
public sealed class TlsReaderWriterTests
{
    [TestMethod]
    public void WriterAndReaderRoundTripEveryIntegerWidth()
    {
        TlsWriter writer = new();
        writer.WriteUInt8(0x01);
        writer.WriteUInt16(0x0203);
        writer.WriteUInt24(0x040506);
        writer.WriteUInt32(0x0708090a);
        byte[] bytes = writer.ToArray();

        TlsReader reader = new(bytes);

        Assert.AreEqual("0102030405060708090a", Convert.ToHexStringLower(bytes));
        Assert.AreEqual(0x01, reader.ReadUInt8());
        Assert.AreEqual(0x0203, reader.ReadUInt16());
        Assert.AreEqual(0x040506, reader.ReadUInt24());
        Assert.AreEqual(0x0708090au, reader.ReadUInt32());
        Assert.IsFalse(reader.HasMore);
        Assert.IsTrue(reader.Finish(0).Succeeded);
    }

    [TestMethod]
    [DataRow(1, 255)]
    [DataRow(2, 65535)]
    public void WriteVectorAcceptsTheLongestBodyItsLengthCanState(int lengthBytes, int length)
    {
        TlsWriter writer = new();

        writer.WriteOpaque(lengthBytes, new byte[length]);

        Assert.HasCount(lengthBytes + length, writer.ToArray());
    }

    [TestMethod]
    [DataRow(1, 256)]
    [DataRow(2, 65536)]
    public void WriteVectorRejectsABodyTooLongForItsLength(int lengthBytes, int length)
    {
        TlsWriter writer = new();

        Assert.ThrowsExactly<ArgumentException>(() => writer.WriteOpaque(lengthBytes, new byte[length]));
    }

    [TestMethod]
    public void AFailureInsideAVectorFailsTheWholeRead()
    {
        TlsReader reader = new(Convert.FromHexString("000301020304"));
        TlsReader vector = reader.ReadVector(2);

        vector.ReadUInt32();

        Assert.IsFalse(vector.HasMore);
        Assert.AreEqual(TlsAlertDescription.DecodeError, reader.Finish(0).Alert);
    }

    [TestMethod]
    public void ReadsAfterAFailureReturnZeroAndEmptyAndKeepTheFirstAlert()
    {
        TlsReader reader = new([0x01]);
        reader.Fail(TlsAlertDescription.IllegalParameter);

        Assert.AreEqual(0, reader.ReadUInt8());
        Assert.IsEmpty(reader.ReadBytes(1));
        Assert.IsEmpty(reader.ReadVector(1).ReadRemaining());
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, reader.Finish(0).Alert);
    }

    [TestMethod]
    public void AVectorLengthPastTheEndIsADecodeErrorAndAnEmptyVector()
    {
        TlsReader reader = new(Convert.FromHexString("0501"));

        TlsReader vector = reader.ReadVector(1);

        Assert.IsFalse(vector.HasMore);
        Assert.AreEqual(TlsAlertDescription.DecodeError, reader.Finish(0).Alert);
    }

    [TestMethod]
    public void DecodeResultValueThrowsWhenTheBytesDidNotDecode()
    {
        TlsDecodeResult<int> failure = TlsDecodeResult<int>.Failure(TlsAlertDescription.DecodeError);
        TlsDecodeResult<int> success = TlsDecodeResult<int>.Success(3);

        Assert.IsFalse(failure.Succeeded);
        Assert.AreEqual(TlsAlertDescription.DecodeError, failure.Alert);
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => failure.Value);
        Assert.Contains("DecodeError", exception.Message);
        Assert.IsTrue(success.Succeeded);
        Assert.IsNull(success.Alert);
        Assert.AreEqual(3, success.Value);
    }
}
