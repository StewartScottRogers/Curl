using Curl.Testing;
using static Curl.Quic.QuicTest;

namespace Curl.Quic;

/// <summary>
/// Checks <see cref="QuicVariableLengthInteger" /> against the sample encodings of RFC 9000
/// section 16 and at each length's boundaries.
/// </summary>
[TestClass]
public sealed class QuicVariableLengthIntegerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("c2197c5eff14e88c", 151288809941952652UL, DisplayName = "RFC 9000 section 16: eight bytes")]
    [DataRow("9d7f3e7d", 494878333UL, DisplayName = "RFC 9000 section 16: four bytes")]
    [DataRow("7bbd", 15293UL, DisplayName = "RFC 9000 section 16: two bytes")]
    [DataRow("25", 37UL, DisplayName = "RFC 9000 section 16: one byte")]
    public void TryRead_Rfc9000Section16Sample_ReadsTheValueAndWritesItBack(string hex, ulong expected)
    {
        Diagnostics.Arrange("encoded hex", hex);
        Diagnostics.Arrange("expected value", expected);

        var readOk = QuicVariableLengthInteger.TryRead(Hex(hex), out var value, out var bytesRead);

        Diagnostics.Act("TryRead result", readOk);
        Diagnostics.Act("value", value);
        Diagnostics.Act("bytes read", bytesRead);
        Diagnostics.Assert("TryRead result", true, readOk);
        Assert.IsTrue(readOk);
        Diagnostics.Assert("value", expected, value);
        Assert.AreEqual(expected, value);
        Diagnostics.Assert("bytes read", hex.Length / 2, bytesRead);
        Assert.AreEqual(hex.Length / 2, bytesRead);

        var written = new byte[8];
        var bytesWritten = QuicVariableLengthInteger.Write(value, written);
        Diagnostics.Act("bytes written", bytesWritten);
        Diagnostics.Bytes("written", written);
        Diagnostics.Assert("bytes written", bytesRead, bytesWritten);
        Assert.AreEqual(bytesRead, bytesWritten);
        var writtenHex = HexOf(written.AsMemory(0, bytesRead));
        Diagnostics.Diff("written hex", hex, writtenHex);
        Assert.AreEqual(hex, writtenHex);
    }

    [TestMethod]
    public void TryRead_TwoByteEncodingOf37_IsAcceptedAlthoughNotShortest()
    {
        Diagnostics.Arrange("encoded hex", "4025");

        var readOk = QuicVariableLengthInteger.TryRead(Hex("4025"), out var value, out var bytesRead);

        Diagnostics.Act("TryRead result", readOk);
        Diagnostics.Act("value", value);
        Diagnostics.Act("bytes read", bytesRead);
        Diagnostics.Assert("TryRead result", true, readOk);
        Assert.IsTrue(readOk);
        Diagnostics.Assert("value", 37UL, value);
        Assert.AreEqual(37UL, value);
        Diagnostics.Assert("bytes read", 2, bytesRead);
        Assert.AreEqual(2, bytesRead);
    }

    [TestMethod]
    public void TryRead_Empty_IsFalse()
    {
        Diagnostics.Arrange("encoded hex", "(empty)");

        var readOk = QuicVariableLengthInteger.TryRead([], out var value, out var bytesRead);

        Diagnostics.Act("TryRead result", readOk);
        Diagnostics.Act("value", value);
        Diagnostics.Act("bytes read", bytesRead);
        Diagnostics.Assert("TryRead result", false, readOk);
        Assert.IsFalse(readOk);
        Diagnostics.Assert("value", 0UL, value);
        Assert.AreEqual(0UL, value);
        Diagnostics.Assert("bytes read", 0, bytesRead);
        Assert.AreEqual(0, bytesRead);
    }

    [TestMethod]
    public void TryRead_EndingInsideTheEncoding_IsFalse()
    {
        Diagnostics.Arrange("encoded hex (truncated four-byte form)", "9d7f3e");

        var readOk = QuicVariableLengthInteger.TryRead(Hex("9d7f3e"), out _, out _);

        Diagnostics.Act("TryRead result", readOk);
        Diagnostics.Assert("TryRead result", false, readOk);
        Assert.IsFalse(readOk);
    }

    [TestMethod]
    [DataRow(0UL, 1)]
    [DataRow(63UL, 1)]
    [DataRow(64UL, 2)]
    [DataRow(16383UL, 2)]
    [DataRow(16384UL, 4)]
    [DataRow(1073741823UL, 4)]
    [DataRow(1073741824UL, 8)]
    [DataRow(QuicVariableLengthInteger.MaximumValue, 8)]
    public void GetEncodedLength_AtEachBoundary_IsTheShortestLength(ulong value, int expected)
    {
        Diagnostics.Arrange("value", value);

        var actual = QuicVariableLengthInteger.GetEncodedLength(value);

        Diagnostics.Act("encoded length", actual);
        Diagnostics.Assert("encoded length", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void GetEncodedLength_AboveTheMaximum_Throws()
    {
        Diagnostics.Arrange("value", QuicVariableLengthInteger.MaximumValue + 1);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicVariableLengthInteger.GetEncodedLength(QuicVariableLengthInteger.MaximumValue + 1));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.Message}");
        Diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void Write_WithALongerLength_WritesThatLength()
    {
        Diagnostics.Arrange("value", 37);
        Diagnostics.Arrange("encoded length", 8);
        var written = new byte[8];

        var count = QuicVariableLengthInteger.Write(37, 8, written);

        Diagnostics.Act("bytes written", count);
        Diagnostics.Bytes("written", written);
        Diagnostics.Assert("bytes written", 8, count);
        Assert.AreEqual(8, count);
        Diagnostics.Diff("written hex", "c000000000000025", HexOf(written));
        Assert.AreEqual("c000000000000025", HexOf(written));
    }

    [TestMethod]
    [DataRow(1, DisplayName = "shorter than the value needs")]
    [DataRow(3, DisplayName = "not a power of two")]
    [DataRow(16, DisplayName = "longer than eight")]
    public void Write_WithALengthTheValueCannotHave_Throws(int encodedLength)
    {
        Diagnostics.Arrange("value", 64);
        Diagnostics.Arrange("encoded length", encodedLength);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicVariableLengthInteger.Write(64, encodedLength, new byte[16]));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.Message}");
        Diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void Write_IntoTooShortADestination_Throws()
    {
        Diagnostics.Arrange("value", 15293);
        Diagnostics.Arrange("destination length", 1);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => QuicVariableLengthInteger.Write(15293, new byte[1]));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.Message}");
        Diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }
}
