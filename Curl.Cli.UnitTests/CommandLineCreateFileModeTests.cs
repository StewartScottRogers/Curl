using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--create-file-mode</c> through the whole parser: an octal value up to
/// <c>0777</c> is recorded as a <see cref="UnixFileMode"/>, the last one given wins, it is
/// not given unless asked for, and a refused value exits 2 with curl 8.21.0's text.
/// </summary>
[TestClass]
public sealed class CommandLineCreateFileModeTests
{
    [TestMethod]
    public void Parse_NoCreateFileMode_LeavesItNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["file:///tmp/upload"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.CreateFileMode);
    }

    [TestMethod]
    [DataRow("0600", UnixFileMode.UserRead | UnixFileMode.UserWrite)]
    [DataRow("777", (UnixFileMode)0b111_111_111)]
    [DataRow("0", UnixFileMode.None)]
    public void Parse_OctalCreateFileMode_RecordsTheMode(string value, UnixFileMode expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--create-file-mode", value]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.CreateFileMode);
    }

    [TestMethod]
    public void Parse_CreateFileModeWithEquals_RecordsTheMode()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--create-file-mode=0640"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual((UnixFileMode)0b110_100_000, result.Options.CreateFileMode);
    }

    [TestMethod]
    public void Parse_CreateFileModeTwice_KeepsTheLast()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--create-file-mode", "0600", "--create-file-mode", "0644"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual((UnixFileMode)0b110_100_100, result.Options.CreateFileMode);
    }

    [TestMethod]
    [DataRow("abc", "curl: option --create-file-mode: expected a proper numerical parameter")]
    [DataRow("8", "curl: option --create-file-mode: expected a proper numerical parameter")]
    [DataRow("", "curl: option --create-file-mode: expected a proper numerical parameter")]
    [DataRow("1000", "curl: option --create-file-mode: too large number")]
    public void Parse_CreateFileModeRefused_ExitsTwoWithCurlsText(string value, string expectedFirstLine)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--create-file-mode", value]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_CreateFileModeAsLastArgument_RefusesAsRequiringParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["file:///tmp/upload", "--create-file-mode"]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --create-file-mode: requires parameter", result.Refusal.StandardErrorLines[0]);
    }
}
