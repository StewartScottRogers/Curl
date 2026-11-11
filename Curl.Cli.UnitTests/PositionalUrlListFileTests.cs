using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins a non-option <c>@file</c> argument as a URL-list file read exactly as <c>--url @file</c> is,
/// as curl 8.13 and later read it (BL-4907): each URL line saved under its remote name, and a file
/// that cannot be opened refused with exit 26 naming the argument as typed.
/// </summary>
[TestClass]
public sealed class PositionalUrlListFileTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_PositionalAtFile_AddsEachUrlLineUsingTheRemoteName()
    {
        RecordingDataFileReader reader = new() { Files = { ["urls.txt"] = "http://h/u1.txt\nhttp://h/u2.txt\n"u8.ToArray() } };

        CommandLineParseResult result = Parse(["-s", "@urls.txt"], reader);

        AssertUrls(["http://h/u1.txt", "http://h/u2.txt"], result);
        Assert.IsTrue(result.Options!.UrlOutputs.All(output => output.UsesRemoteName));
        CollectionAssert.AreEqual(new[] { "urls.txt" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_PositionalAtFileAfterEndOfOptions_AddsEachUrlLine()
    {
        RecordingDataFileReader reader = new() { Files = { ["urls.txt"] = "http://h/u1.txt\n"u8.ToArray() } };

        CommandLineParseResult result = Parse(["--", "@urls.txt"], reader);

        AssertUrls(["http://h/u1.txt"], result);
    }

    [TestMethod]
    public void Parse_PositionalAtMissingFile_IsRefusedWithReadErrorNamingTheArgument()
    {
        CommandLineParseResult result = Parse(["-s", "@missing.txt"], new RecordingDataFileReader());

        Diagnostics.Assert("exit code", CurlExitCode.ReadError, result.Refusal?.ExitCode);
        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option @missing.txt: error encountered when reading a file", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

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
