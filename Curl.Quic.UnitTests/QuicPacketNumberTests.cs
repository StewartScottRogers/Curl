using Curl.Testing;

namespace Curl.Quic;

/// <summary>
/// Checks <see cref="QuicPacketNumber" /> against the examples of RFC 9000 section 17.1 and
/// Appendix A.3, and each way decoding can move the candidate.
/// </summary>
[TestClass]
public sealed class QuicPacketNumberTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Decode_Rfc9000AppendixA3Example_IsA82F9B32()
    {
        Assert.AreEqual(0xa82f9b32UL, DecodeWithDiagnostics(0xa82f9b32UL, 0xa82f30ea, 0x9b32, 2));
    }

    [TestMethod]
    public void Decode_Rfc9001AppendixA5PacketNumber_Is654360564()
    {
        Assert.AreEqual(654360564UL, DecodeWithDiagnostics(654360564UL, 654360563, 49140, 3));
    }

    [TestMethod]
    public void Decode_CandidateFarBelowTheExpected_MovesUpAWindow()
    {
        Assert.AreEqual(0x200UL, DecodeWithDiagnostics(0x200UL, 0x1fe, 0x00, 1));
    }

    [TestMethod]
    public void Decode_CandidateFarAboveTheExpected_MovesDownAWindow()
    {
        Assert.AreEqual(0xffUL, DecodeWithDiagnostics(0xffUL, 0xff, 0xff, 1));
    }

    [TestMethod]
    public void Decode_CandidateAboveTheExpectedButInTheFirstWindow_StaysPut()
    {
        Assert.AreEqual(0xffUL, DecodeWithDiagnostics(0xffUL, null, 0xff, 1));
    }

    [TestMethod]
    public void Decode_CandidateThatWouldPassTheLargestPacketNumber_StaysPut()
    {
        Assert.AreEqual(0x3fff_ffff_ffff_ff00UL, DecodeWithDiagnostics(0x3fff_ffff_ffff_ff00UL, 0x3fff_ffff_ffff_fffd, 0x00, 1));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(5)]
    public void Decode_LengthOutsideOneToFour_Throws(int length)
    {
        Diagnostics.Arrange("length", length);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketNumber.Decode(null, 0, length));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.Message}");
        Diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(0xac5c02UL, 2, DisplayName = "RFC 9000 section 17.1: 16 bits")]
    [DataRow(0xace8feUL, 3, DisplayName = "RFC 9000 section 17.1: 24 bits")]
    public void GetEncodedLength_Rfc9000Section17Point1Example(ulong fullPacketNumber, int expected)
    {
        Diagnostics.Arrange("full packet number", fullPacketNumber);
        Diagnostics.Arrange("largest acknowledged", 0xabe8b3);

        var actual = QuicPacketNumber.GetEncodedLength(fullPacketNumber, 0xabe8b3);

        Diagnostics.Act("encoded length", actual);
        Diagnostics.Assert("encoded length", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(0UL, 1)]
    [DataRow(127UL, 1)]
    [DataRow(128UL, 2)]
    [DataRow(0x800000UL, 4)]
    public void GetEncodedLength_NothingAcknowledged_CountsEveryPacketFromZero(ulong fullPacketNumber, int expected)
    {
        Diagnostics.Arrange("full packet number", fullPacketNumber);
        Diagnostics.Arrange("largest acknowledged", "none");

        var actual = QuicPacketNumber.GetEncodedLength(fullPacketNumber, null);

        Diagnostics.Act("encoded length", actual);
        Diagnostics.Assert("encoded length", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void GetEncodedLength_AcknowledgedNotBelowTheNumberSent_Throws()
    {
        Diagnostics.Arrange("full packet number", 5);
        Diagnostics.Arrange("largest acknowledged", 5);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketNumber.GetEncodedLength(5, 5));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.Message}");
        Diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void GetEncodedLength_MoreThan2To31Unacknowledged_Throws()
    {
        Diagnostics.Arrange("full packet number", 1UL << 31);
        Diagnostics.Arrange("largest acknowledged", "none");

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketNumber.GetEncodedLength(1UL << 31, null));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.Message}");
        Diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    [TestMethod]
    public void Truncate_KeepsTheLowBytes()
    {
        Diagnostics.Arrange("full packet number", 0xa82f9b32UL);
        Diagnostics.Arrange("length", 2);

        var actual = QuicPacketNumber.Truncate(0xa82f9b32, 2);

        Diagnostics.Act("truncated", actual);
        Diagnostics.Assert("truncated", 0x9b32u, actual);
        Assert.AreEqual(0x9b32u, actual);
    }

    [TestMethod]
    public void Truncate_LengthAboveFour_Throws()
    {
        Diagnostics.Arrange("full packet number", 1);
        Diagnostics.Arrange("length", 5);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketNumber.Truncate(1, 5));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.Message}");
        Diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    private ulong DecodeWithDiagnostics(ulong expected, ulong? largestAcknowledged, uint truncated, int length)
    {
        Diagnostics.Arrange("largest acknowledged", largestAcknowledged?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none");
        Diagnostics.Arrange("truncated packet number", truncated);
        Diagnostics.Arrange("length", length);

        var actual = QuicPacketNumber.Decode(largestAcknowledged, truncated, length);

        Diagnostics.Act("decoded", actual);
        Diagnostics.Assert("decoded", expected, actual);
        return actual;
    }
}
