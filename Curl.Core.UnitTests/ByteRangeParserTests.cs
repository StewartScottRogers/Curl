using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Pins how <see cref="ByteRangeParser" /> reads <c>-r</c>/<c>--range</c> text, each case
/// measured against curl 8.21.0 over <c>file://</c> on 2026-09-26.
/// </summary>
[TestClass]
public sealed class ByteRangeParserTests
{
    [TestMethod]
    [DataRow("0-4", 0L, 4L)]
    [DataRow("0-0", 0L, 0L)]
    [DataRow("2-3,5-6", 2L, 3L)]
    [DataRow("1-2abc", 1L, 2L)]
    [DataRow("20-30", 20L, 30L)]
    [DataRow("1-9223372036854775807", 1L, long.MaxValue)]
    public void TryParse_FirstAndLast_IsBounded(string rangeText, long first, long last)
    {
        Assert.IsTrue(ByteRangeParser.TryParse(rangeText, out ByteRange? range));
        Assert.AreEqual(ByteRange.Bounded(first, last), range);
    }

    [TestMethod]
    [DataRow("5-", 5L)]
    [DataRow("5-abc", 5L)]
    [DataRow("3--1", 3L)]
    [DataRow("0-99999999999999999999", 0L)]
    [DataRow("9223372036854775807-", long.MaxValue)]
    [DataRow("-", 0L)]
    [DataRow("-99999999999999999999", 0L)]
    public void TryParse_NoLastPosition_IsFromOffset(string rangeText, long first)
    {
        Assert.IsTrue(ByteRangeParser.TryParse(rangeText, out ByteRange? range));
        Assert.AreEqual(ByteRange.FromOffset(first), range);
    }

    [TestMethod]
    [DataRow("-3", 3L)]
    [DataRow("-3-1", 3L)]
    [DataRow("-20", 20L)]
    public void TryParse_NoFirstPosition_IsSuffix(string rangeText, long suffixLength)
    {
        Assert.IsTrue(ByteRangeParser.TryParse(rangeText, out ByteRange? range));
        Assert.AreEqual(ByteRange.Suffix(suffixLength), range);
    }

    [TestMethod]
    [DataRow("3-1")]
    [DataRow("abc")]
    [DataRow("-0")]
    [DataRow("a-3")]
    [DataRow(" 1-2")]
    [DataRow("+1-2")]
    [DataRow("5")]
    [DataRow("")]
    [DataRow("99999999999999999999")]
    [DataRow("9223372036854775808-")]
    [DataRow("0-9223372036854775807")]
    public void TryParse_TextNamingNoRange_ReturnsFalseWithoutThrowing(string rangeText)
    {
        Assert.IsFalse(ByteRangeParser.TryParse(rangeText, out ByteRange? range));
        Assert.IsNull(range);
    }

    [TestMethod]
    public void TryParse_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ByteRangeParser.TryParse(null!, out _));
    }

    [TestMethod]
    public void NotDeliveredFailure_IsExit33WithCurlsMessage()
    {
        TransferResult failure = ByteRangeParser.NotDeliveredFailure;

        Assert.AreEqual(CurlExitCode.RangeError, failure.ExitCode);
        Assert.AreEqual(33, (int)failure.ExitCode);
        Assert.AreEqual("Requested range was not delivered by the server", failure.ErrorMessage);
        Assert.AreEqual(0L, failure.BytesTransferred);
    }
}
