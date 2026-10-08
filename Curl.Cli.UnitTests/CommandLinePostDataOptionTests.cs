using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--data-ascii</c>, <c>--data-binary</c>, <c>--data-raw</c>, <c>--data-urlencode</c> and
/// <c>--json</c>. Measured with the local curl 8.21.0 on 2026-09-26 against a loopback server, reading
/// the request body (<c>curl -sS http://127.0.0.1:18188/p &lt;arguments&gt;</c> in Git Bash, a file
/// <c>f.txt</c> holding <c>a b</c>, CR LF, <c>c</c>, LF):
/// <c>--data-urlencode 'a b&amp;c=d'</c> sends <c>a b&amp;c=d</c>; <c>'=a b&amp;c=d'</c> sends
/// <c>a+b%26c%3Dd</c>; <c>'n=a b&amp;c=d'</c> sends <c>n=a+b%26c%3Dd</c>; <c>@f.txt</c> sends
/// <c>a+b%0D%0Ac%0A</c>; <c>n@f.txt</c> sends <c>n=a+b%0D%0Ac%0A</c>; <c>a=b@c</c> sends <c>a=b%40c</c>;
/// <c>f.txt@f.txt=x</c> sends <c>f.txt@f.txt=x</c>; <c>n@-</c> with <c>q r</c> on standard input sends
/// <c>n=q+r</c>; <c>n=</c> sends <c>n=</c>; <c>n@empty.txt</c> sends nothing, and between <c>x</c> and
/// <c>y</c> sends <c>x&amp;&amp;y</c>; <c>'n=x+y' '~'</c> sends <c>n=x%2By&amp;~</c>.
/// <c>--data-binary @f.txt --data-binary x</c> sends the file unchanged then <c>&amp;x</c>;
/// <c>--data-raw @f.txt --data-raw '' --data-raw y</c> sends <c>@f.txt&amp;&amp;y</c>;
/// <c>--data-ascii @f.txt</c> sends <c>a bc</c>. <c>--json '{"a":1}' --json '{"b":2}'</c> sends
/// <c>{"a":1}{"b":2}</c>; <c>--json @j.json --json ''</c> sends the file unchanged, CR LF included;
/// <c>-d x --json y</c> sends <c>xy</c>; <c>--json y -d x</c> sends <c>y&amp;x</c>. Each option with a
/// missing file exits 26 with <c>curl: Failed to open missing</c>,
/// <c>curl: option --&lt;name&gt;: error encountered when reading a file</c> and the try-help line;
/// as the last argument each exits 2 with <c>requires parameter</c>.
/// </summary>
[TestClass]
public sealed class CommandLinePostDataOptionTests
{
    private const string Url = "http://127.0.0.1:18188/p";

    private static readonly byte[] FileWithLineBreaks = "a b\r\nc\n"u8.ToArray();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("a b&c=d", "a b&c=d")]
    [DataRow("=a b&c=d", "a+b%26c%3Dd")]
    [DataRow("n=a b&c=d", "n=a+b%26c%3Dd")]
    [DataRow("a=b@c", "a=b%40c")]
    [DataRow("f.txt@f.txt=x", "f.txt@f.txt=x")]
    [DataRow("n=", "n=")]
    [DataRow("=", "")]
    [DataRow("", "")]
    [DataRow("abc", "abc")]
    [DataRow("a b", "a+b")]
    [DataRow("~-_.*!", "~-_.%2A%21")]
    public void Parse_DataUrlencodeText_EncodesAsCurlDoes(string value, string expectedBody)
    {
        CommandLineParseResult result = Parse(["--data-urlencode", value, Url], new RecordingDataFileReader());

        Assert.IsTrue(result.IsAccepted);
        AssertBodyDiagnostic(expectedBody, result);
        Assert.AreEqual(expectedBody, BodyText(result));
    }

    [TestMethod]
    public void Parse_DataUrlencodeAtFile_EncodesTheWholeFile()
    {
        RecordingDataFileReader reader = new() { Files = { ["f.txt"] = FileWithLineBreaks } };

        CommandLineParseResult result = Parse(["--data-urlencode", "@f.txt", Url], reader);

        AssertBodyDiagnostic("a+b%0D%0Ac%0A", result);
        Assert.AreEqual("a+b%0D%0Ac%0A", BodyText(result));
        CollectionAssert.AreEqual(new[] { "f.txt" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_DataUrlencodeNameAtFile_PutsTheNameBeforeTheEncodedFile()
    {
        RecordingDataFileReader reader = new() { Files = { ["f.txt"] = FileWithLineBreaks } };

        CommandLineParseResult result = Parse(["--data-urlencode", "n@f.txt", Url], reader);

        AssertBodyDiagnostic("n=a+b%0D%0Ac%0A", result);
        Assert.AreEqual("n=a+b%0D%0Ac%0A", BodyText(result));
    }

    [TestMethod]
    public void Parse_DataUrlencodeNameAtDash_EncodesStandardInput()
    {
        RecordingDataFileReader reader = new() { StandardInput = "q r"u8.ToArray() };

        CommandLineParseResult result = Parse(["--data-urlencode", "n@-", Url], reader);

        AssertBodyDiagnostic("n=q+r", result);
        Assert.AreEqual("n=q+r", BodyText(result));
        CollectionAssert.AreEqual(new[] { "-" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_DataUrlencodeNameAtEmptyFile_AddsAnEmptyPieceWithoutTheName()
    {
        RecordingDataFileReader reader = new() { Files = { ["empty.txt"] = [] } };

        CommandLineParseResult result = Parse(
            ["--data-urlencode", "x", "--data-urlencode", "n@empty.txt", "--data-urlencode", "y", Url], reader);

        AssertBodyDiagnostic("x&&y", result);
        Assert.AreEqual("x&&y", BodyText(result));
    }

    [TestMethod]
    public void Parse_DataUrlencodeNonAsciiText_EncodesItsUtf8Bytes()
    {
        CommandLineParseResult result = Parse(["--data-urlencode", "é", Url], new RecordingDataFileReader());

        AssertBodyDiagnostic("%C3%A9", result);
        Assert.AreEqual("%C3%A9", BodyText(result));
    }

    [TestMethod]
    public void Parse_DataUrlencodeTwice_JoinsWithAmpersand()
    {
        CommandLineParseResult result = Parse(["--data-urlencode", "n=x+y", "--data-urlencode", "~", Url], new RecordingDataFileReader());

        AssertBodyDiagnostic("n=x%2By&~", result);
        Assert.AreEqual("n=x%2By&~", BodyText(result));
    }

    [TestMethod]
    public void Parse_DataUrlencodeEmptyThenData_SkipsTheSeparatorAfterAnEmptyBody()
    {
        CommandLineParseResult result = Parse(["--data-urlencode", "=", "-d", "x", "--data-urlencode", "a", Url], new RecordingDataFileReader());

        AssertBodyDiagnostic("x&a", result);
        Assert.AreEqual("x&a", BodyText(result));
    }

    [TestMethod]
    public void Parse_DataBinaryAtFileThenText_KeepsTheFileUnchangedAndJoinsWithAmpersand()
    {
        RecordingDataFileReader reader = new() { Files = { ["f.txt"] = FileWithLineBreaks } };

        CommandLineParseResult result = Parse(["--data-binary", "@f.txt", "--data-binary", "x", Url], reader);

        Assert.IsTrue(result.IsAccepted);
        AssertBodyDiagnostic("a b\r\nc\n&x", result);
        Assert.AreEqual("a b\r\nc\n&x", BodyText(result));
        Assert.IsFalse(result.Options!.SendsJson);
    }

    [TestMethod]
    public void Parse_DataBinaryAtDash_ReadsStandardInputUnchanged()
    {
        RecordingDataFileReader reader = new() { StandardInput = FileWithLineBreaks };

        CommandLineParseResult result = Parse(["--data-binary", "@-", Url], reader);

        AssertBodyDiagnostic("a b\r\nc\n", result);
        Assert.AreEqual("a b\r\nc\n", BodyText(result));
    }

    [TestMethod]
    public void Parse_DataRaw_NeverReadsAFileAndJoinsEmptyPieces()
    {
        RecordingDataFileReader reader = new();

        CommandLineParseResult result = Parse(["--data-raw", "@f.txt", "--data-raw", "", "--data-raw", "y", Url], reader);

        Assert.IsTrue(result.IsAccepted);
        AssertBodyDiagnostic("@f.txt&&y", result);
        Assert.AreEqual("@f.txt&&y", BodyText(result));
        Assert.IsEmpty(reader.Reads);
    }

    [TestMethod]
    public void Parse_DataAsciiAtFile_RemovesLineBreaksAsDataDoes()
    {
        RecordingDataFileReader reader = new() { Files = { ["f.txt"] = FileWithLineBreaks } };

        CommandLineParseResult result = Parse(["--data-ascii", "@f.txt", Url], reader);

        AssertBodyDiagnostic("a bc", result);
        Assert.AreEqual("a bc", BodyText(result));
    }

    [TestMethod]
    public void Parse_JsonTwice_ConcatenatesWithoutSeparatorAndSendsJson()
    {
        CommandLineParseResult result = Parse(["--json", "{\"a\":1}", "--json", "{\"b\":2}", Url], new RecordingDataFileReader());

        Assert.IsTrue(result.IsAccepted);
        AssertBodyDiagnostic("{\"a\":1}{\"b\":2}", result);
        Assert.AreEqual("{\"a\":1}{\"b\":2}", BodyText(result));
        Assert.IsTrue(result.Options!.SendsJson);
    }

    [TestMethod]
    public void Parse_JsonAtFileThenEmpty_KeepsTheFileUnchanged()
    {
        RecordingDataFileReader reader = new() { Files = { ["j.json"] = "{\"a\":1}\r\n"u8.ToArray() } };

        CommandLineParseResult result = Parse(["--json", "@j.json", "--json", "", Url], reader);

        AssertBodyDiagnostic("{\"a\":1}\r\n", result);
        Assert.AreEqual("{\"a\":1}\r\n", BodyText(result));
    }

    [TestMethod]
    public void Parse_DataThenJson_AppendsTheJsonWithoutSeparator()
    {
        CommandLineParseResult result = Parse(["-d", "x", "--json", "y", Url], new RecordingDataFileReader());

        AssertBodyDiagnostic("xy", result);
        Assert.AreEqual("xy", BodyText(result));
        Assert.IsTrue(result.Options!.SendsJson);
    }

    [TestMethod]
    public void Parse_JsonThenData_JoinsTheDataWithAmpersandAndStillSendsJson()
    {
        CommandLineParseResult result = Parse(["--json", "y", "-d", "x", Url], new RecordingDataFileReader());

        AssertBodyDiagnostic("y&x", result);
        Assert.AreEqual("y&x", BodyText(result));
        Assert.IsTrue(result.Options!.SendsJson);
    }

    [TestMethod]
    public void Parse_NoJson_DoesNotSendJson()
    {
        CommandLineParseResult result = Parse(["-d", "x", Url], new RecordingDataFileReader());

        Diagnostics.Assert("sends json", false, result.Options?.SendsJson);
        Assert.IsFalse(result.Options!.SendsJson);
    }

    [TestMethod]
    [DataRow("--data-ascii", "@missing")]
    [DataRow("--data-binary", "@missing")]
    [DataRow("--json", "@missing")]
    [DataRow("--data-urlencode", "@missing")]
    [DataRow("--data-urlencode", "n@missing")]
    public void Parse_AtMissingFile_IsRefusedWithReadError(string option, string value)
    {
        CommandLineParseResult result = Parse([option, value, Url], new RecordingDataFileReader());

        AssertRefusalDiagnostic(CurlExitCode.ReadError, "curl: Failed to open missing", result);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: Failed to open missing",
                $"curl: option {option}: error encountered when reading a file",
                CommandLineRefusal.TryHelpLine,
            },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_SilentBeforeDataUrlencodeAtMissingFile_HidesTheFailedToOpenLine()
    {
        CommandLineParseResult result = Parse(["-s", "--data-urlencode", "n@missing", Url], new RecordingDataFileReader());

        AssertRefusalDiagnostic(CurlExitCode.ReadError, "curl: option --data-urlencode: error encountered when reading a file", result);
        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --data-urlencode: error encountered when reading a file", CommandLineRefusal.TryHelpLine },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--data-ascii")]
    [DataRow("--data-binary")]
    [DataRow("--data-raw")]
    [DataRow("--data-urlencode")]
    [DataRow("--json")]
    [DataRow("--url-query")]
    public void Parse_OptionAsLastArgument_RequiresParameter(string option)
    {
        CommandLineParseResult result = Parse([Url, option], new RecordingDataFileReader());

        AssertRefusalDiagnostic(CurlExitCode.FailedInit, $"curl: option {option}: requires parameter", result);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {option}: requires parameter", CommandLineRefusal.TryHelpLine },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    private static string BodyText(CommandLineParseResult result) =>
        Encoding.UTF8.GetString(result.Options!.PostData!.Value.Span);

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, RecordingDataFileReader reader)
    {
        Diagnostics.ArrangeArguments(arguments);
        foreach ((string path, byte[] contents) in reader.Files.OrderBy(file => file.Key, StringComparer.Ordinal))
        {
            Diagnostics.Bytes($"file {path}", contents);
        }

        Diagnostics.Bytes("standard input", reader.StandardInput);

        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader);

        Diagnostics.ActParse(result);
        Diagnostics.Act("reads", CommandLineParseDiagnostics.QuoteEach(reader.Reads));
        if (result.Options?.PostData is { } postData)
        {
            Diagnostics.Bytes("body", postData.Span);
        }

        return result;
    }

    private void AssertBodyDiagnostic(string expectedBody, CommandLineParseResult result) =>
        Diagnostics.Diff("body", expectedBody, result.Options?.PostData is { } postData ? Encoding.UTF8.GetString(postData.Span) : "<no body>");

    private void AssertRefusalDiagnostic(CurlExitCode expectedExitCode, string expectedFirstLine, CommandLineParseResult result)
    {
        Diagnostics.Assert("exit code", expectedExitCode, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);
        Diagnostics.Assert("first stderr line", expectedFirstLine, CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines[0]);
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
