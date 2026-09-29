using static Curl.Quic.QuicTest;

namespace Curl.Quic;

/// <summary>
/// Checks <see cref="QuicVariableLengthInteger" /> against the sample encodings of RFC 9000
/// section 16 and at each length's boundaries.
/// </summary>
[TestClass]
public sealed class QuicVariableLengthIntegerTests
{
    [TestMethod]
    [DataRow("c2197c5eff14e88c", 151288809941952652UL, DisplayName = "RFC 9000 section 16: eight bytes")]
    [DataRow("9d7f3e7d", 494878333UL, DisplayName = "RFC 9000 section 16: four bytes")]
    [DataRow("7bbd", 15293UL, DisplayName = "RFC 9000 section 16: two bytes")]
    [DataRow("25", 37UL, DisplayName = "RFC 9000 section 16: one byte")]
    public void TryRead_Rfc9000Section16Sample_ReadsTheValueAndWritesItBack(string hex, ulong expected)
    {
        Assert.IsTrue(QuicVariableLengthInteger.TryRead(Hex(hex), out var value, out var bytesRead));
        Assert.AreEqual(expected, value);
        Assert.AreEqual(hex.Length / 2, bytesRead);

        var written = new byte[8];
        Assert.AreEqual(bytesRead, QuicVariableLengthInteger.Write(value, written));
        Assert.AreEqual(hex, HexOf(written.AsMemory(0, bytesRead)));
    }

    [TestMethod]
    public void TryRead_TwoByteEncodingOf37_IsAcceptedAlthoughNotShortest()
    {
        Assert.IsTrue(QuicVariableLengthInteger.TryRead(Hex("4025"), out var value, out var bytesRead));
        Assert.AreEqual(37UL, value);
        Assert.AreEqual(2, bytesRead);
    }

    [TestMethod]
    public void TryRead_Empty_IsFalse()
    {
        Assert.IsFalse(QuicVariableLengthInteger.TryRead([], out var value, out var bytesRead));
        Assert.AreEqual(0UL, value);
        Assert.AreEqual(0, bytesRead);
    }

    [TestMethod]
    public void TryRead_EndingInsideTheEncoding_IsFalse() =>
        Assert.IsFalse(QuicVariableLengthInteger.TryRead(Hex("9d7f3e"), out _, out _));

    [TestMethod]
    [DataRow(0UL, 1)]
    [DataRow(63UL, 1)]
    [DataRow(64UL, 2)]
    [DataRow(16383UL, 2)]
    [DataRow(16384UL, 4)]
    [DataRow(1073741823UL, 4)]
    [DataRow(1073741824UL, 8)]
    [DataRow(QuicVariableLengthInteger.MaximumValue, 8)]
    public void GetEncodedLength_AtEachBoundary_IsTheShortestLength(ulong value, int expected) =>
        Assert.AreEqual(expected, QuicVariableLengthInteger.GetEncodedLength(value));

    [TestMethod]
    public void GetEncodedLength_AboveTheMaximum_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicVariableLengthInteger.GetEncodedLength(QuicVariableLengthInteger.MaximumValue + 1));

    [TestMethod]
    public void Write_WithALongerLength_WritesThatLength()
    {
        var written = new byte[8];

        Assert.AreEqual(8, QuicVariableLengthInteger.Write(37, 8, written));
        Assert.AreEqual("c000000000000025", HexOf(written));
    }

    [TestMethod]
    [DataRow(1, DisplayName = "shorter than the value needs")]
    [DataRow(3, DisplayName = "not a power of two")]
    [DataRow(16, DisplayName = "longer than eight")]
    public void Write_WithALengthTheValueCannotHave_Throws(int encodedLength) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicVariableLengthInteger.Write(64, encodedLength, new byte[16]));

    [TestMethod]
    public void Write_IntoTooShortADestination_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => QuicVariableLengthInteger.Write(15293, new byte[1]));
}
