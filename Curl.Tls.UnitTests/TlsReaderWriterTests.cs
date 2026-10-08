using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Checks the presentation-language primitives under the codecs: fixed-width integers,
/// vectors and their length limits, the sticky failure a <see cref="TlsReader" /> shares
/// with its vectors, and <see cref="TlsDecodeResult{T}" />.
/// </summary>
[TestClass]
public sealed class TlsReaderWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void WriterAndReaderRoundTripEveryIntegerWidth()
    {
        TlsWriter writer = new();
        writer.WriteUInt8(0x01);
        writer.WriteUInt16(0x0203);
        writer.WriteUInt24(0x040506);
        writer.WriteUInt32(0x0708090a);
        byte[] bytes = writer.ToArray();
        Diagnostics.Arrange("written", "uint8 0x01, uint16 0x0203, uint24 0x040506, uint32 0x0708090a");

        TlsReader reader = new(bytes);
        Diagnostics.Bytes("encoded", bytes);
        Diagnostics.Act("reader has more", reader.HasMore);

        Diagnostics.Assert("hex", "0102030405060708090a", Convert.ToHexStringLower(bytes));
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
        Diagnostics.Arrange("length bytes and body length", $"{lengthBytes}, {length}");

        writer.WriteOpaque(lengthBytes, new byte[length]);
        int written = writer.ToArray().Length;
        Diagnostics.Act("written length", written);

        Diagnostics.Assert("written length", lengthBytes + length, written);
        Assert.HasCount(lengthBytes + length, writer.ToArray());
    }

    [TestMethod]
    [DataRow(1, 256)]
    [DataRow(2, 65536)]
    public void WriteVectorRejectsABodyTooLongForItsLength(int lengthBytes, int length)
    {
        TlsWriter writer = new();
        Diagnostics.Arrange("length bytes and body length", $"{lengthBytes}, {length}");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => writer.WriteOpaque(lengthBytes, new byte[length]));
        Diagnostics.Act("thrown", exception.GetType().Name);

        Diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void AFailureInsideAVectorFailsTheWholeRead()
    {
        TlsReader reader = new(Convert.FromHexString("000301020304"));
        TlsReader vector = reader.ReadVector(2);
        Diagnostics.Arrange("input", "000301020304, a 2-byte length vector of 3 bytes");

        vector.ReadUInt32();
        TlsAlertDescription? alert = reader.Finish(0).Alert;
        Diagnostics.Act("alert", alert);

        Diagnostics.Assert("alert", TlsAlertDescription.DecodeError, alert);
        Assert.IsFalse(vector.HasMore);
        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void ReadsAfterAFailureReturnZeroAndEmptyAndKeepTheFirstAlert()
    {
        TlsReader reader = new([0x01]);
        reader.Fail(TlsAlertDescription.IllegalParameter);
        Diagnostics.Arrange("reader", "one byte 01, failed with IllegalParameter");
        Diagnostics.Act("reads", "uint8, bytes(1), vector(1), then finish");

        Assert.AreEqual(0, reader.ReadUInt8());
        Assert.IsEmpty(reader.ReadBytes(1));
        Assert.IsEmpty(reader.ReadVector(1).ReadRemaining());
        TlsAlertDescription? alert = reader.Finish(0).Alert;
        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, alert);
        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert);
    }

    [TestMethod]
    public void AVectorLengthPastTheEndIsADecodeErrorAndAnEmptyVector()
    {
        TlsReader reader = new(Convert.FromHexString("0501"));
        Diagnostics.Arrange("input", "0501, a 1-byte length of 5 with 1 byte left");

        TlsReader vector = reader.ReadVector(1);
        Diagnostics.Act("vector has more", vector.HasMore);

        TlsAlertDescription? alert = reader.Finish(0).Alert;
        Diagnostics.Assert("alert", TlsAlertDescription.DecodeError, alert);
        Assert.IsFalse(vector.HasMore);
        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void DecodeResultValueThrowsWhenTheBytesDidNotDecode()
    {
        TlsDecodeResult<int> failure = TlsDecodeResult<int>.Failure(TlsAlertDescription.DecodeError);
        TlsDecodeResult<int> success = TlsDecodeResult<int>.Success(3);
        Diagnostics.Arrange("results", "Failure(DecodeError) and Success(3)");
        Diagnostics.Act("succeeded", $"failure {failure.Succeeded}, success {success.Succeeded}");

        Assert.IsFalse(failure.Succeeded);
        Assert.AreEqual(TlsAlertDescription.DecodeError, failure.Alert);
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => failure.Value);
        Diagnostics.Assert("exception message contains", true, exception.Message.Contains("DecodeError", StringComparison.Ordinal));
        Assert.Contains("DecodeError", exception.Message);
        Assert.IsTrue(success.Succeeded);
        Assert.IsNull(success.Alert);
        Assert.AreEqual(3, success.Value);
    }
}
