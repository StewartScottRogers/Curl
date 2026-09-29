namespace Curl.Quic;

/// <summary>
/// Checks <see cref="QuicPacketNumber" /> against the examples of RFC 9000 section 17.1 and
/// Appendix A.3, and each way decoding can move the candidate.
/// </summary>
[TestClass]
public sealed class QuicPacketNumberTests
{
    [TestMethod]
    public void Decode_Rfc9000AppendixA3Example_IsA82F9B32() =>
        Assert.AreEqual(0xa82f9b32UL, QuicPacketNumber.Decode(0xa82f30ea, 0x9b32, 2));

    [TestMethod]
    public void Decode_Rfc9001AppendixA5PacketNumber_Is654360564() =>
        Assert.AreEqual(654360564UL, QuicPacketNumber.Decode(654360563, 49140, 3));

    [TestMethod]
    public void Decode_CandidateFarBelowTheExpected_MovesUpAWindow() =>
        Assert.AreEqual(0x200UL, QuicPacketNumber.Decode(0x1fe, 0x00, 1));

    [TestMethod]
    public void Decode_CandidateFarAboveTheExpected_MovesDownAWindow() =>
        Assert.AreEqual(0xffUL, QuicPacketNumber.Decode(0xff, 0xff, 1));

    [TestMethod]
    public void Decode_CandidateAboveTheExpectedButInTheFirstWindow_StaysPut() =>
        Assert.AreEqual(0xffUL, QuicPacketNumber.Decode(null, 0xff, 1));

    [TestMethod]
    public void Decode_CandidateThatWouldPassTheLargestPacketNumber_StaysPut() =>
        Assert.AreEqual(0x3fff_ffff_ffff_ff00UL, QuicPacketNumber.Decode(0x3fff_ffff_ffff_fffd, 0x00, 1));

    [TestMethod]
    [DataRow(0)]
    [DataRow(5)]
    public void Decode_LengthOutsideOneToFour_Throws(int length) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketNumber.Decode(null, 0, length));

    [TestMethod]
    [DataRow(0xac5c02UL, 2, DisplayName = "RFC 9000 section 17.1: 16 bits")]
    [DataRow(0xace8feUL, 3, DisplayName = "RFC 9000 section 17.1: 24 bits")]
    public void GetEncodedLength_Rfc9000Section17Point1Example(ulong fullPacketNumber, int expected) =>
        Assert.AreEqual(expected, QuicPacketNumber.GetEncodedLength(fullPacketNumber, 0xabe8b3));

    [TestMethod]
    [DataRow(0UL, 1)]
    [DataRow(127UL, 1)]
    [DataRow(128UL, 2)]
    [DataRow(0x800000UL, 4)]
    public void GetEncodedLength_NothingAcknowledged_CountsEveryPacketFromZero(ulong fullPacketNumber, int expected) =>
        Assert.AreEqual(expected, QuicPacketNumber.GetEncodedLength(fullPacketNumber, null));

    [TestMethod]
    public void GetEncodedLength_AcknowledgedNotBelowTheNumberSent_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketNumber.GetEncodedLength(5, 5));

    [TestMethod]
    public void GetEncodedLength_MoreThan2To31Unacknowledged_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketNumber.GetEncodedLength(1UL << 31, null));

    [TestMethod]
    public void Truncate_KeepsTheLowBytes() =>
        Assert.AreEqual(0x9b32u, QuicPacketNumber.Truncate(0xa82f9b32, 2));

    [TestMethod]
    public void Truncate_LengthAboveFour_Throws() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicPacketNumber.Truncate(1, 5));
}
