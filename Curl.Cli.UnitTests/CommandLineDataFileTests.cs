using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-d @file</c> and <c>-d @-</c>. Measured with the local curl 8.21.0 on 2026-09-26
/// (<c>MSYS_NO_PATHCONV=1 curl &lt;arguments&gt; http://127.0.0.1:1/</c> in Git Bash): a file holding
/// <c>61 0D 0A 62 0A 00 63</c> sent with <c>-G -d @file</c> gives the query <c>abc</c>; standard input
/// <c>q r</c> and a line feed sent with <c>-G -d @-</c> gives <c>q r</c>; <c>-d @missing</c> exits 26
/// with <c>curl: Failed to open missing</c>, <c>curl: option -d: error encountered when reading a file</c>
/// and the try-help line, the option named as typed (<c>--data</c>, <c>--data=@missing</c>,
/// <c>-d@missing</c>); <c>-s -d @missing</c> drops the first line, <c>-s -S -d @missing</c> and
/// <c>-d @missing -s</c> keep it.
/// </summary>
[TestClass]
public sealed class CommandLineDataFileTests
{
    private const string Url = "http://example.com/";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Parse_DataAtFile_ReadsTheFileWithoutCarriageReturnsLineFeedsAndNuls()
    {
        RecordingDataFileReader reader = new() { Files = { ["body.txt"] = [0x61, 0x0D, 0x0A, 0x62, 0x0A, 0x00, 0x63] } };

        CommandLineParseResult result = Parse(["-d", "@body.txt", Url], reader);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        AssertPostData("abc"u8.ToArray(), result);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0x62, 0x63 }, result.Options.PostData!.Value.ToArray());
        CollectionAssert.AreEqual(new[] { "body.txt" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_DataAtDash_ReadsStandardInputWithoutLineBreaks()
    {
        RecordingDataFileReader reader = new() { StandardInput = [0x71, 0x20, 0x72, 0x0A] };

        CommandLineParseResult result = Parse(["--data", "@-", Url], reader);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        AssertPostData("q r"u8.ToArray(), result);
        CollectionAssert.AreEqual(new byte[] { 0x71, 0x20, 0x72 }, result.Options.PostData!.Value.ToArray());
        CollectionAssert.AreEqual(new[] { "-" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_DataWithoutAt_NeverCallsTheReader()
    {
        RecordingDataFileReader reader = new();

        CommandLineParseResult result = Parse(["-d", "abc", Url], reader);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        AssertPostData("abc"u8.ToArray(), result);
        CollectionAssert.AreEqual(new byte[] { 0x61, 0x62, 0x63 }, result.Options.PostData!.Value.ToArray());
        Assert.IsEmpty(reader.Reads);
    }

    [TestMethod]
    public void Parse_DataAtFileAfterText_JoinsWithAmpersand()
    {
        RecordingDataFileReader reader = new() { Files = { ["b"] = [0x62, 0x0A] } };

        CommandLineParseResult result = Parse(["-d", "a", "-d@b", Url], reader);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        AssertPostData("a&b"u8.ToArray(), result);
        CollectionAssert.AreEqual("a&b"u8.ToArray(), result.Options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_DataAtFileHoldingOnlyLineBreaks_IsEmptyData()
    {
        RecordingDataFileReader reader = new() { Files = { ["blank"] = [0x0D, 0x0A] } };

        CommandLineParseResult result = Parse(["-d", "@blank", Url], reader);

        TestDiagnostics.For(TestContext).Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        AssertPostData([], result);
        Assert.AreEqual(0, result.Options.PostData!.Value.Length);
    }

    [TestMethod]
    public void Parse_DataAtMissingFile_IsRefusedWithReadErrorInCurlsThreeLines()
    {
        CommandLineParseResult result = Parse(["-d", "@missing", Url], new RecordingDataFileReader());

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("accepted", false, result.IsAccepted);
        diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Refusal?.ExitCode);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: Failed to open missing",
                "curl: option -d: error encountered when reading a file",
                "curl: try 'curl --help' or 'curl --manual' for more information",
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--data=@missing")]
    [DataRow("-d@missing")]
    public void Parse_DataAtMissingFileAttached_NamesTheOptionAsTyped(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url], new RecordingDataFileReader());

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Refusal?.ExitCode);
        diagnostics.Assert(
            "second stderr line",
            $"curl: option {argument}: error encountered when reading a file",
            result.Refusal?.StandardErrorLines.ElementAtOrDefault(1));
        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        Assert.AreEqual($"curl: option {argument}: error encountered when reading a file", result.Refusal!.StandardErrorLines[1]);
    }

    [TestMethod]
    public void Parse_SilentBeforeDataAtMissingFile_HidesTheFailedToOpenLine()
    {
        CommandLineParseResult result = Parse(["-s", "-d", "@missing", Url], new RecordingDataFileReader());

        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Refusal?.ExitCode);
        diagnostics.Assert("stderr line count", 2, result.Refusal?.StandardErrorLines.Count);
        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: option -d: error encountered when reading a file",
                "curl: try 'curl --help' or 'curl --manual' for more information",
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_NullDataFileReader_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeArguments([Url]);
        diagnostics.Arrange("data file reader", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineParser.Parse([Url], _ => true, new UnexpectedPasswordPrompt(), null!));

        diagnostics.Act("exception", $"{exception.GetType().Name} for {exception.ParamName}");
        diagnostics.Assert("parameter name", "dataFileReader", exception.ParamName);
        Assert.AreEqual("dataFileReader", exception.ParamName);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, RecordingDataFileReader reader)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeArguments(arguments);
        foreach (KeyValuePair<string, byte[]> file in reader.Files)
        {
            diagnostics.Bytes($"file {file.Key}", file.Value);
        }

        diagnostics.Bytes("standard input", reader.StandardInput);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader);
        diagnostics.ActParse(result);
        diagnostics.Act("reads", CommandLineParseDiagnostics.QuoteEach(reader.Reads));
        return result;
    }

    private void AssertPostData(byte[] expected, CommandLineParseResult result)
    {
        byte[] actual = result.Options?.PostData?.ToArray() ?? [];
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("post data", actual);
        diagnostics.Diff("post data", expected, actual);
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
