using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-X</c>/<c>--request</c>, <c>-H</c>/<c>--header</c>, <c>-A</c>/<c>--user-agent</c> and
/// <c>-e</c>/<c>--referer</c>. Measured with the local curl 8.21.0 (mingw, Schannel) on 2026-09-26 in
/// Git Bash, against <c>http://127.0.0.1:1/</c> for standard error and exit codes and against a
/// loopback listener on port 18787 for the request bytes:
/// <list type="bullet">
/// <item><c>-X ''</c> and <c>--request ''</c> exit 2 with <c>curl: option -X: blank argument where content is expected</c> and the try-help line.</item>
/// <item><c>-H foo</c>, <c>-H 'foo bar'</c>, <c>-H -x</c> and <c>-H ''</c> warn
/// <c>Warning: The provided HTTP header 'foo' does not look like a header?</c> and carry on; <c>-H 'foo;'</c>,
/// <c>-H ':'</c> and <c>-H 'X: y'</c> do not warn. <c>-s -H foo</c> and <c>-s -S -H foo</c> drop the warning;
/// <c>-H foo -s</c> keeps it.</item>
/// <item><c>-H 'X: 1' -H 'X: 2'</c> sends <c>X: 1</c> then <c>X: 2</c>. A file holding
/// <c>A: 1\r\nB: 2\n\n\r\n  C: 3\nnocolon\nD;\n\nE: 4</c> sent with <c>-H @file</c> adds its non-empty lines in
/// order, <c>  C: 3</c> with its leading spaces, and warns about none; <c>-H @-</c> reads standard input.</item>
/// <item><c>-H @nosuch</c> exits 26 with <c>curl: Failed to open nosuch</c>,
/// <c>curl: option -H: error encountered when reading a file</c> and the try-help line, the option named as
/// typed (<c>--header=@nosuch</c>, <c>-H@nosuch</c>); <c>-s -H @nosuch</c> drops the first line and
/// <c>-s -S -H @nosuch</c> keeps it.</item>
/// <item><c>-A ''</c> and <c>-e ''</c> are accepted (the request then has no <c>User-Agent</c> header);
/// <c>-e ';auto'</c> is accepted; <c>-A -x</c>, <c>-e -x</c> and <c>-X -x</c> take <c>-x</c> as the value without a warning.</item>
/// <item><c>--no-request</c>, <c>--no-header</c>, <c>--no-header=x</c>, <c>--no-user-agent</c> and
/// <c>--no-referer</c> exit 2 with <c>curl: option &lt;as typed&gt;: the given option cannot be reversed with a --no- prefix</c>.</item>
/// </list>
/// </summary>
[TestClass]
public sealed class CommandLineHttpRequestOptionTests
{
    private const string Url = "http://example.com/";

    [TestMethod]
    [DataRow("-X", "PUT")]
    [DataRow("--request", "DELETE")]
    public void Parse_Request_KeepsTheMethodVerbatim(string option, string method)
    {
        CommandLineParseResult result = Parse([option, method, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(method, result.Options.RequestMethod);
    }

    [TestMethod]
    public void Parse_RequestTwice_KeepsTheLast()
    {
        CommandLineParseResult result = Parse(["-X", "PUT", "-X", "patch", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("patch", result.Options.RequestMethod);
    }

    [TestMethod]
    public void Parse_NoRequest_LeavesTheMethodUnset()
    {
        CommandLineParseResult result = Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.RequestMethod);
        Assert.IsNull(result.Options.UserAgent);
        Assert.IsNull(result.Options.Referer);
        Assert.IsEmpty(result.Options.Headers);
    }

    [TestMethod]
    [DataRow("-X")]
    [DataRow("--request")]
    public void Parse_EmptyRequest_IsRefusedAsBlank(string option)
    {
        CommandLineParseResult result = Parse([option, "", Url]);

        AssertRefused(result, CurlExitCode.FailedInit, $"curl: option {option}: blank argument where content is expected");
    }

    [TestMethod]
    [DataRow("-A", "-x")]
    [DataRow("-e", "-x")]
    [DataRow("-X", "-x")]
    public void Parse_ValueThatLooksLikeAFlag_IsTakenWithoutAWarning(string option, string value)
    {
        CommandLineParseResult result = Parse([option, value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_SeveralHeaders_KeepsThemVerbatimInOrder()
    {
        CommandLineParseResult result = Parse(["-H", "X: 1", "--header", "X: 2", "-H", "Y:", "--header=Z;", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "X: 1", "X: 2", "Y:", "Z;" }, result.Options.Headers.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("foo")]
    [DataRow("foo bar")]
    [DataRow("-x")]
    [DataRow("")]
    public void Parse_HeaderWithoutColonOrSemicolon_WarnsAndKeepsIt(string header)
    {
        CommandLineParseResult result = Parse(["-H", header, Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { header }, result.Options.Headers.ToArray());
        CollectionAssert.AreEqual(
            new[] { $"Warning: The provided HTTP header '{header}' does not look like a header?" },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow(":")]
    [DataRow("foo;")]
    public void Parse_HeaderWithColonOrSemicolon_DoesNotWarn(string header)
    {
        CommandLineParseResult result = Parse(["-H", header, Url]);

        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_SilentBeforeHeaderWithoutColon_DropsTheWarning()
    {
        CommandLineParseResult result = Parse(["-s", "-S", "-H", "foo", Url]);

        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_SilentAfterHeaderWithoutColon_KeepsTheWarning()
    {
        CommandLineParseResult result = Parse(["-H", "foo", "-s", Url]);

        Assert.HasCount(1, result.WarningLines);
    }

    [TestMethod]
    public void Parse_HeaderAtFile_AddsEachNonEmptyLineVerbatimThroughTheReader()
    {
        RecordingDataFileReader reader = new() { Files = { ["h.txt"] = "A: 1\r\nB: 2\n\n\r\n  C: 3\nnocolon\nD;\n\nE: 4"u8.ToArray() } };

        CommandLineParseResult result = Parse(["-H", "X: 0", "-H", "@h.txt", "-H", "F: 5", Url], reader);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "X: 0", "A: 1", "B: 2", "  C: 3", "nocolon", "D;", "E: 4", "F: 5" },
            result.Options.Headers.ToArray());
        CollectionAssert.AreEqual(new[] { "h.txt" }, reader.Reads);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_HeaderAtEmptyFile_AddsNothing()
    {
        RecordingDataFileReader reader = new() { Files = { ["empty"] = [] } };

        CommandLineParseResult result = Parse(["--header", "@empty", Url], reader);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.Options.Headers);
    }

    [TestMethod]
    public void Parse_HeaderAtDash_ReadsStandardInput()
    {
        RecordingDataFileReader reader = new() { StandardInput = "X: from-stdin\n"u8.ToArray() };

        CommandLineParseResult result = Parse(["-H", "@-", Url], reader);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "X: from-stdin" }, result.Options.Headers.ToArray());
        CollectionAssert.AreEqual(new[] { "-" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_HeaderAtMissingFile_IsRefusedWithReadErrorInCurlsThreeLines()
    {
        CommandLineParseResult result = Parse(["-H", "@nosuch", Url]);

        AssertRefused(result, CurlExitCode.ReadError, "curl: Failed to open nosuch", "curl: option -H: error encountered when reading a file");
    }

    [TestMethod]
    [DataRow("--header=@nosuch")]
    [DataRow("-H@nosuch")]
    public void Parse_HeaderAtMissingFileAttached_NamesTheOptionAsTyped(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        AssertRefused(result, CurlExitCode.ReadError, "curl: Failed to open nosuch", $"curl: option {argument}: error encountered when reading a file");
    }

    [TestMethod]
    public void Parse_HeaderAtEmptyFileName_NamesTheEmptyFile()
    {
        CommandLineParseResult result = Parse(["-H", "@", Url]);

        AssertRefused(result, CurlExitCode.ReadError, "curl: Failed to open ", "curl: option -H: error encountered when reading a file");
    }

    [TestMethod]
    public void Parse_SilentBeforeHeaderAtMissingFile_HidesTheFailedToOpenLine()
    {
        CommandLineParseResult result = Parse(["-s", "-H", "@nosuch", Url]);

        AssertRefused(result, CurlExitCode.ReadError, "curl: option -H: error encountered when reading a file");
    }

    [TestMethod]
    public void Parse_SilentAndShowErrorBeforeHeaderAtMissingFile_KeepsTheFailedToOpenLine()
    {
        CommandLineParseResult result = Parse(["-s", "-S", "-H", "@nosuch", Url]);

        AssertRefused(result, CurlExitCode.ReadError, "curl: Failed to open nosuch", "curl: option -H: error encountered when reading a file");
    }

    [TestMethod]
    [DataRow("-A", "agent/1.0")]
    [DataRow("--user-agent", "")]
    public void Parse_UserAgent_KeepsTheValueVerbatimEmptyIncluded(string option, string userAgent)
    {
        CommandLineParseResult result = Parse([option, userAgent, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(userAgent, result.Options.UserAgent);
    }

    [TestMethod]
    [DataRow("-e", "http://r.example/x")]
    [DataRow("--referer", "http://r.example/x")]
    [DataRow("-e", "")]
    [DataRow("-e", "http://r.example/;Auto")]
    [DataRow("-e", "http://r.example/;auto/x")]
    public void Parse_Referer_KeepsTheValueAsGivenWithoutAutoReferer(string option, string referer)
    {
        CommandLineParseResult result = Parse([option, referer, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(referer, result.Options.Referer);
        Assert.IsFalse(result.Options.AutoReferer);
    }

    [TestMethod]
    [DataRow("-e")]
    [DataRow("--referer")]
    public void Parse_RefererEndingInAuto_StripsTheSuffixAndTurnsOnAutoReferer(string option)
    {
        CommandLineParseResult result = Parse([option, "http://r.example/x;auto", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("http://r.example/x", result.Options.Referer);
        Assert.IsTrue(result.Options.AutoReferer);
    }

    [TestMethod]
    [DataRow("-e")]
    [DataRow("--referer")]
    public void Parse_RefererAutoAlone_SetsNoRefererAndTurnsOnAutoReferer(string option)
    {
        CommandLineParseResult result = Parse([option, ";auto", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.Referer);
        Assert.IsTrue(result.Options.AutoReferer);
    }

    [TestMethod]
    public void Parse_RefererWithoutAutoAfterOneWithIt_TurnsAutoRefererOff()
    {
        CommandLineParseResult result = Parse(["-e", ";auto", "-e", "http://r.example/x", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("http://r.example/x", result.Options.Referer);
        Assert.IsFalse(result.Options.AutoReferer);
    }

    [TestMethod]
    public void Parse_NoRequest_IsRefusedAsNotReversible()
    {
        AssertCannotBeReversed("--no-request");
    }

    [TestMethod]
    public void Parse_NoHeader_IsRefusedAsNotReversible()
    {
        AssertCannotBeReversed("--no-header");
    }

    [TestMethod]
    public void Parse_NoHeaderWithValue_IsRefusedAsNotReversible()
    {
        AssertCannotBeReversed("--no-header=x");
    }

    [TestMethod]
    public void Parse_NoUserAgent_IsRefusedAsNotReversible()
    {
        AssertCannotBeReversed("--no-user-agent");
    }

    [TestMethod]
    public void Parse_NoReferer_IsRefusedAsNotReversible()
    {
        AssertCannotBeReversed("--no-referer");
    }

    [TestMethod]
    public void HeaderDoesNotLookLikeAHeader_NullHeader_Throws()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineWarning.HeaderDoesNotLookLikeAHeader(null!));

        Assert.AreEqual("header", exception.ParamName);
    }

    private static void AssertCannotBeReversed(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        AssertRefused(result, CurlExitCode.FailedInit, $"curl: option {argument}: the given option cannot be reversed with a --no- prefix");
    }

    private static void AssertRefused(CommandLineParseResult result, CurlExitCode exitCode, params string[] linesBeforeTryHelp)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(exitCode, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            (string[])[.. linesBeforeTryHelp, CommandLineRefusal.TryHelpLine],
            result.Refusal.StandardErrorLines.ToArray());
    }

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        Parse(arguments, new RecordingDataFileReader());

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, IDataFileReader reader) =>
        CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader);

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
