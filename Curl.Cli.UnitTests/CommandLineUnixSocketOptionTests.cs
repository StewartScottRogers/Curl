using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--unix-socket</c> and <c>--abstract-unix-socket</c>. Measured with the
/// local curl 8.21.0 (Schannel) on 2026-09-28 through <c>Record-CurlExchange.ps1</c> against
/// <c>http://127.0.0.1:45506/</c>; the bytes are in BL-506's Notes. Both are accepted at parse time on
/// Windows, and fail only when the transfer dials the socket (exit 7).
/// </summary>
[TestClass]
public sealed class CommandLineUnixSocketOptionTests
{
    private const string Url = "http://127.0.0.1:45506/";

    [TestMethod]
    public void Parse_NoUnixSocketOptions_RecordsNoSocket()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.UnixSocketPath);
        Assert.IsFalse(result.Options.UnixSocketIsAbstract);
    }

    [TestMethod]
    [DataRow(new[] { "--unix-socket", "nosuch.sock" }, "nosuch.sock", false)]
    [DataRow(new[] { "--unix-socket=/run/x.sock" }, "/run/x.sock", false)]
    [DataRow(new[] { "--abstract-unix-socket", "x" }, "x", true)]
    [DataRow(new[] { "--abstract-unix-socket=x" }, "x", true)]
    [DataRow(new[] { "--unix-socket", "a", "--unix-socket", "b" }, "b", false)]
    [DataRow(new[] { "--unix-socket", "a", "--abstract-unix-socket", "b" }, "b", true)]
    [DataRow(new[] { "--abstract-unix-socket", "a", "--unix-socket", "b" }, "b", false)]
    [DataRow(new[] { "--abstract-unix-socket", "a", "--abstract-unix-socket", "b" }, "b", true)]
    public void Parse_UnixSocketOptions_RecordTheLastPathAndWhetherItIsAbstract(string[] arguments, string path, bool isAbstract)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(path, result.Options.UnixSocketPath);
        Assert.AreEqual(isAbstract, result.Options.UnixSocketIsAbstract);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow(new[] { "--unix-socket", "" }, "--unix-socket")]
    [DataRow(new[] { "--abstract-unix-socket", "" }, "--abstract-unix-socket")]
    [DataRow(new[] { "--unix-socket", "nosuch.sock", "--abstract-unix-socket", "" }, "--abstract-unix-socket")]
    [DataRow(new[] { "--abstract-unix-socket", "x", "--unix-socket", "" }, "--unix-socket")]
    [DataRow(new[] { "--unix-socket", "nosuch.sock", "--unix-socket", "" }, "--unix-socket")]
    public void Parse_UnixSocketOptionWithEmptyPath_RefusesAsBlank(string[] arguments, string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--no-unix-socket")]
    [DataRow("--no-abstract-unix-socket")]
    public void Parse_NoPrefixedUnixSocketOption_RefusesTheNoPrefix(string option)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, "x", Url]);

        AssertRefused(result, $"curl: option {option}: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    [DataRow("--unix-socket", false)]
    [DataRow("--abstract-unix-socket", true)]
    public void Parse_UnixSocketGivenFlagLikePath_WarnsAndKeepsThePath(string option, bool isAbstract)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, "-s", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-s", result.Options.UnixSocketPath);
        Assert.AreEqual(isAbstract, result.Options.UnixSocketIsAbstract);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-s' looks like a flag." },
            result.WarningLines.ToArray());
    }

    private static void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
