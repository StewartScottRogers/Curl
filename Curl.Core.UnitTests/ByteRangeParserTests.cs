using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins how <see cref="ByteRangeParser" /> reads <c>-r</c>/<c>--range</c> text, each case
/// measured against curl 8.21.0 over <c>file://</c> on 2026-09-26.
/// </summary>
[TestClass]
public sealed class ByteRangeParserTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("0-4", 0L, 4L)]
    [DataRow("0-0", 0L, 0L)]
    [DataRow("2-3,5-6", 2L, 3L)]
    [DataRow("1-2abc", 1L, 2L)]
    [DataRow("20-30", 20L, 30L)]
    [DataRow("1-9223372036854775807", 1L, long.MaxValue)]
    public void TryParse_FirstAndLast_IsBounded(string rangeText, long first, long last)
    {
        var expected = ByteRange.Bounded(first, last);

        var parsed = Parse(rangeText, out ByteRange? range, true, expected);

        Assert.IsTrue(parsed);
        Assert.AreEqual(expected, range);
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
        var expected = ByteRange.FromOffset(first);

        var parsed = Parse(rangeText, out ByteRange? range, true, expected);

        Assert.IsTrue(parsed);
        Assert.AreEqual(expected, range);
    }

    [TestMethod]
    [DataRow("-3", 3L)]
    [DataRow("-3-1", 3L)]
    [DataRow("-20", 20L)]
    public void TryParse_NoFirstPosition_IsSuffix(string rangeText, long suffixLength)
    {
        var expected = ByteRange.Suffix(suffixLength);

        var parsed = Parse(rangeText, out ByteRange? range, true, expected);

        Assert.IsTrue(parsed);
        Assert.AreEqual(expected, range);
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
        var parsed = Parse(rangeText, out ByteRange? range, false, null);

        Assert.IsFalse(parsed);
        Assert.IsNull(range);
    }

    [TestMethod]
    public void TryParse_Null_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("call", "TryParse(null)");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ByteRangeParser.TryParse(null!, out _));

        diagnostics.Act("exception", exception.GetType().Name + " (" + exception.ParamName + ")");
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void NotDeliveredFailure_IsExit33WithCurlsMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("member", nameof(ByteRangeParser.NotDeliveredFailure));

        TransferResult failure = ByteRangeParser.NotDeliveredFailure;

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("error message", failure.ErrorMessage);
        diagnostics.Act("bytes transferred", failure.BytesTransferred);
        diagnostics.Assert("exit code", 33, (int)failure.ExitCode);
        diagnostics.Assert("error message", "Requested range was not delivered by the server", failure.ErrorMessage);
        diagnostics.Assert("bytes transferred", 0L, failure.BytesTransferred);
        Assert.AreEqual(CurlExitCode.RangeError, failure.ExitCode);
        Assert.AreEqual(33, (int)failure.ExitCode);
        Assert.AreEqual("Requested range was not delivered by the server", failure.ErrorMessage);
        Assert.AreEqual(0L, failure.BytesTransferred);
    }

    private bool Parse(string rangeText, out ByteRange? range, bool expectedParsed, ByteRange? expectedRange)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range text", "\"" + rangeText + "\"");

        var parsed = ByteRangeParser.TryParse(rangeText, out range);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("range", range?.ToString() ?? "(null)");
        diagnostics.Assert("parsed", expectedParsed, parsed);
        diagnostics.Assert("range", expectedRange?.ToString() ?? "(null)", range?.ToString() ?? "(null)");
        return parsed;
    }
}
