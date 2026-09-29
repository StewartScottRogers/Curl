using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--keepalive-time</c> and <c>--keepalive-cnt</c>. Measured against the
/// local curl 8.21.0 (Schannel, Windows) on 2026-09-29 through <c>Record-CurlExchange.ps1</c> and
/// <c>--libcurl</c>; every case, with the bytes curl printed, is in BL-645's Notes.
/// </summary>
[TestClass]
public sealed class CommandLineKeepAliveTimerTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    [TestMethod]
    public void Parse_NeitherOption_LeavesBothZeroForLibcurlsDefaults()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(0L, result.Options.TcpKeepAliveSeconds);
        Assert.AreEqual(0L, result.Options.TcpKeepAliveProbeCount);
    }

    [TestMethod]
    [DataRow("5", 5L)]
    [DataRow("0", 0L)]
    [DataRow("-0", 0L)]
    [DataRow("2147483647", 2147483647L)]
    public void Parse_KeepAliveTime_RecordsTheSeconds(string value, long expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--keepalive-time", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.TcpKeepAliveSeconds);
        Assert.AreEqual(0L, result.Options.TcpKeepAliveProbeCount);
    }

    [TestMethod]
    [DataRow("3", 3L)]
    [DataRow("0", 0L)]
    public void Parse_KeepAliveCount_RecordsTheProbeCount(string value, long expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--keepalive-cnt", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.TcpKeepAliveProbeCount);
        Assert.AreEqual(0L, result.Options.TcpKeepAliveSeconds);
    }

    [TestMethod]
    public void Parse_EachOptionTwice_LastOneWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--keepalive-time", "5", "--keepalive-cnt", "3", "--keepalive-time=7", "--keepalive-cnt=4", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(7L, result.Options.TcpKeepAliveSeconds);
        Assert.AreEqual(4L, result.Options.TcpKeepAliveProbeCount);
    }

    [TestMethod]
    [DataRow("--keepalive-time", "-1", "expected a positive numerical parameter")]
    [DataRow("--keepalive-time", "abc", "expected a proper numerical parameter")]
    [DataRow("--keepalive-time", "1.5", "expected a proper numerical parameter")]
    [DataRow("--keepalive-cnt", "-1", "expected a positive numerical parameter")]
    [DataRow("--keepalive-cnt", "abc", "expected a proper numerical parameter")]
    [DataRow("--keepalive-cnt", "", "expected a proper numerical parameter")]
    public void Parse_BadValue_RefusedAsCurlRefusesIt(string option, string value, string reason)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, value, Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { $"curl: option {option}: {reason}", TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("--keepalive-time")]
    [DataRow("--keepalive-cnt")]
    public void Parse_PastA32BitLongOnWindows_RefusedAsNotAProperNumber(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, "2147483648", Url]);

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {option}: expected a proper numerical parameter", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("--keepalive-time")]
    [DataRow("--keepalive-cnt")]
    public void Parse_PastA32BitLongOffWindows_Accepted(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, "2147483648", Url]);

        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    [DataRow("--no-keepalive-time")]
    [DataRow("--no-keepalive-cnt")]
    public void Parse_NoPrefix_RefusedAsCannotBeReversed(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, "5", Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            CommandLineRefusal.CannotBeReversed(spelling).StandardErrorLines.ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
