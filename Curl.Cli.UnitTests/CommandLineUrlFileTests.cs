using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--url @file</c> and <c>--url @-</c> as curl 8.21.0's <c>parse_url</c> reads them (BL-1804,
/// upstream tests 488, 489 and 2012): one URL per line, blank and <c>#</c> lines skipped, each URL
/// saved under its remote name unless a <c>-o</c> is paired with it, and a file that cannot be opened
/// refused with exit 26 and no <c>Failed to open</c> line.
/// </summary>
[TestClass]
public sealed class CommandLineUrlFileTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_UrlAtFile_AddsEachUrlLineUsingTheRemoteName()
    {
        RecordingDataFileReader reader = new() { Files = { ["urls"] = "http://h/a\r\n\n  \t\n  # comment\nhttp://h/b"u8.ToArray() } };

        CommandLineParseResult result = Parse(["--url", "@urls"], reader);

        AssertUrls(["http://h/a", "http://h/b"], result);
        Assert.IsTrue(result.Options!.UrlOutputs.All(output => output.UsesRemoteName));
        CollectionAssert.AreEqual(new[] { "urls" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_UrlAtFileBesidePositionalUrl_MarksOnlyTheFileUrlUnglobbed()
    {
        RecordingDataFileReader reader = new() { Files = { ["urls"] = "http://h/{a,b}\n"u8.ToArray() } };

        CommandLineParseResult result = Parse(["--url", "@urls", "http://h/{c,d}"], reader);

        AssertUrls(["http://h/{a,b}", "http://h/{c,d}"], result);
        bool[] unglobbed = result.Options!.UrlOutputs.Select(output => output.IsUnglobbed).ToArray();
        Diagnostics.Assert("unglobbed", "True, False", string.Join(", ", unglobbed));
        CollectionAssert.AreEqual(new[] { true, false }, unglobbed);
    }

    [TestMethod]
    public void Parse_UrlAtDash_ReadsStandardInput()
    {
        RecordingDataFileReader reader = new() { StandardInput = "http://h/a\nhttp://h/b\n"u8.ToArray() };

        CommandLineParseResult result = Parse(["--output-dir", "out", "--url", "@-"], reader);

        AssertUrls(["http://h/a", "http://h/b"], result);
        CollectionAssert.AreEqual(new[] { "-" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_UrlAtFileWithOutputs_PairsEachOutputWithAUrlInOrder()
    {
        RecordingDataFileReader reader = new() { Files = { ["urls"] = "http://h/1\nhttp://h/2\n"u8.ToArray() } };

        CommandLineParseResult result = Parse(["-T", "up{1,2}", "--url", "@urls", "--output=first", "--output=second"], reader);

        AssertUrls(["http://h/1", "http://h/2"], result);
        CollectionAssert.AreEqual(new[] { "first", "second" }, result.Options!.UrlOutputs.Select(output => output.FileName).ToArray());
        CollectionAssert.AreEqual(new[] { "up{1,2}" }, result.Options.UploadFiles.ToArray());
    }

    [TestMethod]
    public void Parse_UrlAtMissingFile_IsRefusedWithReadErrorAndNoFailedToOpenLine()
    {
        CommandLineParseResult result = Parse(["--url", "@nosuch"], new RecordingDataFileReader());

        Diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Refusal?.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --url: error encountered when reading a file", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_UrlAtFileBesideEtagSave_RefusesTheSecondUrl()
    {
        RecordingDataFileReader reader = new() { Files = { ["urls"] = "http://h/1\nhttp://h/2\n"u8.ToArray() } };

        CommandLineParseResult result = Parse(["--etag-save", "e.txt", "--url", "@urls"], reader);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
    }

    [TestMethod]
    public void UrlFileUnreadable_NullSpelledOption_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.UrlFileUnreadable(null!));

    private void AssertUrls(string[] expected, CommandLineParseResult result)
    {
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(result.Options?.Urls ?? []));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(expected, result.Options.Urls.ToArray());
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, IDataFileReader reader)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader);
        Diagnostics.ActParse(result);
        return result;
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
