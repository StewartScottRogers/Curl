using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>-z</c>/<c>--time-cond</c>, against curl 8.21.0 measured on
/// Windows on 2026-09-26: a leading <c>-</c> inverts the condition, <c>+</c> and <c>=</c> are
/// dropped, and a value that is not a date is warned about, not refused, and leaves no condition.
/// </summary>
[TestClass]
public sealed class CommandLineTimeConditionOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    [TestMethod]
    public void Parse_NoTimeCond_HasNoTimeCondition()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.TimeCondition);
    }

    [TestMethod]
    public void Parse_TimeCondDate_IsIfModifiedSinceThatDate()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-z", "1 Jan 2030", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(
            new TimeCondition(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfModifiedSince),
            result.Options.TimeCondition);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_TimeCondDashDate_IsIfUnmodifiedSinceThatDate()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--time-cond", "-1 Jan 2000", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(
            new TimeCondition(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfUnmodifiedSince),
            result.Options.TimeCondition);
    }

    [TestMethod]
    public void Parse_TimeCondRfc1123Date_ReadsThatInstant()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-z", "Sun, 06 Nov 1994 08:49:37 GMT", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(new DateTimeOffset(1994, 11, 6, 8, 49, 37, TimeSpan.Zero), result.Options.TimeCondition?.Value);
    }

    /// <summary>curl 8.21.0: <c>-z "+1 Jan 2030"</c> and <c>-z "=1 Jan 2030"</c> both skip a 2026 file as not new enough.</summary>
    /// <param name="value">The value after <c>-z</c>.</param>
    [TestMethod]
    [DataRow("+1 Jan 2030")]
    [DataRow("=1 Jan 2030")]
    public void Parse_TimeCondPlusOrEqualsDate_IsIfModifiedSinceThatDate(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse([$"--time-cond={value}", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(
            new TimeCondition(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), TimeConditionKind.IfModifiedSince),
            result.Options.TimeCondition);
    }

    /// <summary>
    /// curl 8.21.0, <c>curl -z notadate -o NUL file:///Z:/repos/Curl.lanes/lane-3/global.json</c>:
    /// these two lines on standard error, then the transfer, exit 0. <c>-z -notadate</c> prints the same.
    /// </summary>
    /// <param name="value">The value after <c>-z</c>.</param>
    [TestMethod]
    [DataRow("notadate")]
    [DataRow("-notadate")]
    public void Parse_TimeCondNotADate_WarnsWithCurlsLinesAndHasNoTimeCondition(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-z", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.TimeCondition);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Illegal date format for -z, --time-cond (and not a filename). ",
                "Warning: Disabling time condition. See curl_getdate(3) for valid date syntax.",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_TimeCondDateThenNotADate_HasNoTimeCondition()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-z", "1 Jan 2030", "-z", "notadate", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.TimeCondition);
    }

    [TestMethod]
    public void Parse_TimeCondNotADateThenDate_HasThatDate()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-z", "notadate", "-z", "-1 Jan 2000", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeConditionKind.IfUnmodifiedSince, result.Options.TimeCondition?.Kind);
        Assert.HasCount(2, result.WarningLines);
    }

    /// <summary>curl 8.21.0: <c>curl -s -z notadate ...</c> prints nothing on standard error.</summary>
    [TestMethod]
    public void Parse_SilentThenTimeCondNotADate_DoesNotWarn()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "-z", "notadate", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_TimeCondAsLastArgument_RequiresAParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, "-z"]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option -z: requires parameter", result.Refusal.StandardErrorLines[0]);
    }

    /// <summary>curl 8.21.0: <c>curl --no-time-cond x ...</c> exits 2 with the cannot-be-reversed line.</summary>
    [TestMethod]
    public void Parse_NoTimeCond_CannotBeReversed()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-time-cond", "x", Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(
            "curl: option --no-time-cond: the given option cannot be reversed with a --no- prefix",
            result.Refusal.StandardErrorLines[0]);
    }
}
