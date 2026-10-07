using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoUnixSocketOptions_RecordsNoSocket()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("unix socket path", null, Recorded(result)?.UnixSocketPath);
        Diagnostics.Assert("unix socket is abstract", false, Recorded(result)?.UnixSocketIsAbstract);
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
        CommandLineParseResult result = Parse([.. arguments, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("unix socket path", path, Recorded(result)?.UnixSocketPath);
        Diagnostics.Assert("unix socket is abstract", isAbstract, Recorded(result)?.UnixSocketIsAbstract);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach([]), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
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
        CommandLineParseResult result = Parse([.. arguments, Url]);

        AssertRefused(result, $"curl: option {spelledOption}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("--no-unix-socket")]
    [DataRow("--no-abstract-unix-socket")]
    public void Parse_NoPrefixedUnixSocketOption_RefusesTheNoPrefix(string option)
    {
        CommandLineParseResult result = Parse([option, "x", Url]);

        AssertRefused(result, $"curl: option {option}: the given option cannot be reversed with a --no- prefix");
    }

    [TestMethod]
    [DataRow("--unix-socket", false)]
    [DataRow("--abstract-unix-socket", true)]
    public void Parse_UnixSocketGivenFlagLikePath_WarnsAndKeepsThePath(string option, bool isAbstract)
    {
        CommandLineParseResult result = Parse([option, "-s", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("unix socket path", "-s", Recorded(result)?.UnixSocketPath);
        Diagnostics.Assert("unix socket is abstract", isAbstract, Recorded(result)?.UnixSocketIsAbstract);
        Diagnostics.Assert(
            "warning lines",
            CommandLineParseDiagnostics.QuoteEach(["Warning: The filename argument '-s' looks like a flag."]),
            CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-s", result.Options.UnixSocketPath);
        Assert.AreEqual(isAbstract, result.Options.UnixSocketIsAbstract);
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-s' looks like a flag." },
            result.WarningLines.ToArray());
    }

    /// <summary>
    /// Returns the parsed options, or null for a refusal, for diagnostic lines written before the test asserts
    /// acceptance, without making the compiler treat <see cref="CommandLineParseResult.Options"/> as possibly null.
    /// </summary>
    private static CommandLineOptions? Recorded(CommandLineParseResult result) => result.Options;

    /// <summary>Parses <paramref name="arguments"/>, writing them, the outcome and the unix socket values as diagnostics.</summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("unix socket path", result.Options.UnixSocketPath);
            Diagnostics.Act("unix socket is abstract", result.Options.UnixSocketIsAbstract);
        }

        return result;
    }

    private void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
