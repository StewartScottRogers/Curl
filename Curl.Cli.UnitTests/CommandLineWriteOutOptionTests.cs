using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-w</c>/<c>--write-out</c>. Measured with the local curl 8.21.0 on 2026-09-26
/// (<c>curl &lt;arguments&gt; --bogus</c> for what parses, and
/// <c>curl -s -w &lt;template&gt; -o /dev/null file:///C:/Windows/win.ini | od -c</c> for the template
/// it keeps): a file holding <c>x%{http_code}</c> and a line feed prints <c>x000</c>; one holding
/// <c>61 0D 0A 62 0A</c> prints <c>ab</c>; one holding <c>61 00 62 0D 63</c> prints <c>abc</c>; standard
/// input <c>q%{http_code}</c> via <c>-w @-</c> prints <c>q000</c>; <c>-w foo -w ''</c> and
/// <c>-w foo -w @empty.txt</c> print nothing. An empty file warns <c>Warning: Failed to read empty.txt</c>
/// (empty standard input <c>Warning: Failed to read &lt;stdin&gt;</c>) unless <c>-s</c> came first; a
/// file holding only <c>0D 0A</c> or only a NUL does not warn. <c>-w @nonexist</c> exits 26 with
/// <c>curl: Failed to open nonexist</c>, <c>curl: option -w: error encountered when reading a file</c>
/// and the try-help line. <c>-w -x</c> does not warn. <c>--no-write-out</c> and <c>--no-write-out=x</c>
/// exit 2 as not reversible.
/// </summary>
[TestClass]
public sealed class CommandLineWriteOutOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string CannotBeReversed = "the given option cannot be reversed with a --no- prefix";

    [TestMethod]
    public void Parse_NoWriteOut_HasNoTemplate()
    {
        CommandLineParseResult result = Parse([Url], new RecordingDataFileReader());

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.WriteOut);
    }

    [TestMethod]
    [DataRow(new[] { "-w", "%{http_code}" }, "%{http_code}")]
    [DataRow(new[] { "--write-out", "%{http_code}" }, "%{http_code}")]
    [DataRow(new[] { "--write-out=%{http_code}" }, "%{http_code}")]
    [DataRow(new[] { "-w%{http_code}" }, "%{http_code}")]
    [DataRow(new[] { "-w", "-x" }, "-x")]
    public void Parse_WriteOutText_KeepsTheTemplateWithoutWarning(string[] arguments, string template)
    {
        RecordingDataFileReader reader = new();

        CommandLineParseResult result = Parse([.. arguments, Url], reader);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(template, result.Options.WriteOut);
        Assert.IsEmpty(result.WarningLines);
        Assert.IsEmpty(reader.Reads);
    }

    [TestMethod]
    public void Parse_WriteOutThenEmptyWriteOut_KeepsTheEmptyTemplate()
    {
        CommandLineParseResult result = Parse(["-w", "foo", "-w", string.Empty, Url], new RecordingDataFileReader());

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.WriteOut);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x78, 0x25, 0x7B, 0x68, 0x7D, 0x0A }, "x%{h}")]
    [DataRow(new byte[] { 0x61, 0x0D, 0x0A, 0x62, 0x0A }, "ab")]
    [DataRow(new byte[] { 0x61, 0x00, 0x62, 0x0D, 0x63 }, "abc")]
    [DataRow(new byte[] { 0x0D, 0x0A }, "")]
    public void Parse_WriteOutAtFile_ReadsTheFileWithoutCarriageReturnsLineFeedsAndNuls(byte[] contents, string template)
    {
        RecordingDataFileReader reader = new() { Files = { ["wo.txt"] = contents } };

        CommandLineParseResult result = Parse(["-w", "@wo.txt", Url], reader);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(template, result.Options.WriteOut);
        Assert.IsEmpty(result.WarningLines);
        CollectionAssert.AreEqual(new[] { "wo.txt" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_WriteOutAtDash_ReadsStandardInput()
    {
        RecordingDataFileReader reader = new() { StandardInput = "q%{http_code}"u8.ToArray() };

        CommandLineParseResult result = Parse(["--write-out", "@-", Url], reader);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("q%{http_code}", result.Options.WriteOut);
        CollectionAssert.AreEqual(new[] { "-" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_WriteOutAtEmptyFile_ClearsTheTemplateAndWarns()
    {
        RecordingDataFileReader reader = new() { Files = { ["empty.txt"] = [] } };

        CommandLineParseResult result = Parse(["-w", "foo", "-w", "@empty.txt", Url], reader);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.WriteOut);
        CollectionAssert.AreEqual(new[] { "Warning: Failed to read empty.txt" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_WriteOutAtEmptyStandardInput_WarnsNamingStdin()
    {
        CommandLineParseResult result = Parse(["-w", "@-", Url], new RecordingDataFileReader());

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.WriteOut);
        CollectionAssert.AreEqual(new[] { "Warning: Failed to read <stdin>" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_SilentThenWriteOutAtEmptyFile_DoesNotWarn()
    {
        RecordingDataFileReader reader = new() { Files = { ["empty.txt"] = [] } };

        CommandLineParseResult result = Parse(["-s", "-w", "@empty.txt", Url], reader);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_WriteOutAtEmptyFileThenSilent_StillWarns()
    {
        RecordingDataFileReader reader = new() { Files = { ["empty.txt"] = [] } };

        CommandLineParseResult result = Parse(["-w", "@empty.txt", "-s", "-S", Url], reader);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "Warning: Failed to read empty.txt" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "-w", "@nonexist" }, "-w", "nonexist")]
    [DataRow(new[] { "--write-out=@nonexist" }, "--write-out=@nonexist", "nonexist")]
    [DataRow(new[] { "-w", "@" }, "-w", "")]
    public void Parse_WriteOutAtUnreadableFile_IsRefusedWithReadError(string[] arguments, string spelledOption, string file)
    {
        CommandLineParseResult result = Parse([.. arguments, Url], new RecordingDataFileReader());

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: Failed to open {file}", $"curl: option {spelledOption}: error encountered when reading a file", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_SilentThenWriteOutAtUnreadableFile_DropsTheFailedToOpenLine()
    {
        CommandLineParseResult result = Parse(["-s", "-w", "@nonexist", Url], new RecordingDataFileReader());

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option -w: error encountered when reading a file", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--no-write-out")]
    [DataRow("--no-write-out=x")]
    public void Parse_NoWriteOut_IsRefusedAsNotReversible(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, Url], new RecordingDataFileReader());

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: {CannotBeReversed}", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void FailedToRead_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineWarning.FailedToRead(null!));

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, IDataFileReader reader) =>
        CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader);

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
