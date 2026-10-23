using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how curl 8.22.0 (Git for Windows' ucrt64 build, Schannel) wraps a long <c>curl:</c> error line,
/// measured on Windows on 2026-10-22 with <c>-sS</c>, a missing file and <c>COLUMNS</c> unset, 40 or 200
/// (BL-3169). Each line holds at most the terminal width, cut after the last space that fits, or inside a
/// token at the width when none does, as <c>voutf</c> in <c>src/tool_msgs.c</c> cuts it.
/// </summary>
[TestClass]
public sealed class CommandLineRefusalWrappingTests
{
    private const string LongPath = "/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate-authority-bundle.pem";
    private const string SpacedPath = "/some dir/with spaces in it/and more words here/to make it/long enough/to wrap/bundle.pem";
    private const string NetrcPath = LongPath + "/extra/segments/to/make/it/much/longer/than/one/whole/line/of/text";
    private const string WidestNetrcPath = NetrcPath + "/and/then/even/more/text/beyond/that/and/more/words/to/pass/two/hundred/bytes";

    [TestMethod]
    [DataRow("--cacert", LongPath, null, "curl: The file \ncurl: '/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate\ncurl: -authority-bundle.pem' provided to --cacert does not exist")]
    [DataRow("--cacert", LongPath, "40", "curl: The file \ncurl: '/some/very/long/missing/path/that\ncurl: /goes/on/and/on/for/a/while/certif\ncurl: icate-authority-bundle.pem' \ncurl: provided to --cacert does not \ncurl: exist")]
    [DataRow("--cacert", LongPath, "200", "curl: The file '/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate-authority-bundle.pem' provided to --cacert does not exist")]
    [DataRow("--cacert", SpacedPath, null, "curl: The file '/some dir/with spaces in it/and more words here/to make \ncurl: it/long enough/to wrap/bundle.pem' provided to --cacert does not exist")]
    [DataRow("--cacert", SpacedPath, "40", "curl: The file '/some dir/with spaces \ncurl: in it/and more words here/to make \ncurl: it/long enough/to \ncurl: wrap/bundle.pem' provided to \ncurl: --cacert does not exist")]
    [DataRow("--proxy-cacert", LongPath, null, "curl: The file \ncurl: '/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate\ncurl: -authority-bundle.pem' provided to --proxy-cacert does not exist")]
    [DataRow("--proxy-cacert", SpacedPath, null, "curl: The file '/some dir/with spaces in it/and more words here/to make \ncurl: it/long enough/to wrap/bundle.pem' provided to --proxy-cacert does not \ncurl: exist")]
    [DataRow("--knownhosts", LongPath, null, "curl: The file \ncurl: '/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate\ncurl: -authority-bundle.pem' provided to --knownhosts does not exist")]
    [DataRow("--netrc-file", NetrcPath, null, "curl: The file \ncurl: '/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate\ncurl: -authority-bundle.pem/extra/segments/to/make/it/much/longer/than/one/whol\ncurl: e/line/of/text' provided to --netrc-file does not exist")]
    [DataRow("--netrc-file", WidestNetrcPath, "200", "curl: The file \ncurl: '/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate-authority-bundle.pem/extra/segments/to/make/it/much/longer/than/one/whole/line/of/text/and/then/even/more/text/beyond/th\ncurl: at/and/more/words/to/pass/two/hundred/bytes' provided to --netrc-file does not exist")]
    [DataRow("--cacert", "/a/b/c/xxxxxxxxxxxxxxx.pem", null, "curl: The file '/a/b/c/xxxxxxxxxxxxxxx.pem' provided to --cacert does not exist")]
    public void FileDoesNotExist_LongFile_WrapsLikeCurl(string longOption, string file, string? columnsVariable, string expectedLines)
    {
        CommandLineRefusal refusal = CommandLineRefusal.FileDoesNotExist(longOption, longOption, file, errorsHidden: false, WrappedMessage.TerminalColumns(columnsVariable));

        CollectionAssert.AreEqual(
            expectedLines.Split('\n').Append($"curl: option {longOption}: is badly used here").Append(CommandLineRefusal.TryHelpLine).ToArray(),
            refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void FileDoesNotExist_LongCacert_KeepsEveryLineWithin79Bytes()
    {
        CommandLineRefusal refusal = CommandLineRefusal.FileDoesNotExist("--cacert", "--cacert", LongPath, errorsHidden: false, WrappedMessage.DefaultTerminalColumns);

        Assert.IsTrue(refusal.StandardErrorLines.All(line => line.Length <= 79));
        Assert.AreEqual(5, refusal.StandardErrorLines.Count);
    }

    [TestMethod]
    public void DataFileUnreadable_LongFile_WrapsLikeCurl()
    {
        CommandLineRefusal refusal = CommandLineRefusal.DataFileUnreadable("-d", LongPath, errorsHidden: false, WrappedMessage.DefaultTerminalColumns);

        CollectionAssert.AreEqual(
            new[]
            {
                "curl: Failed to open ",
                "curl: /some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate-",
                "curl: authority-bundle.pem",
                "curl: option -d: error encountered when reading a file",
                CommandLineRefusal.TryHelpLine,
            },
            refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_CacertMissingLongFileWithColumns40InParseEnvironment_WrapsAt40()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--cacert", LongPath, "http://example.com/"],
            _ => false,
            new UnexpectedPasswordPrompt(),
            new RecordingDataFileReader(),
            new DefaultConfigFileSearch(_ => null, false, null, null),
            false,
            name => name == "COLUMNS" ? "40" : null);

        CollectionAssert.AreEqual(
            "curl: The file \ncurl: '/some/very/long/missing/path/that\ncurl: /goes/on/and/on/for/a/while/certif\ncurl: icate-authority-bundle.pem' \ncurl: provided to --cacert does not \ncurl: exist"
                .Split('\n').Append("curl: option --cacert: is badly used here").Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal!.StandardErrorLines.ToArray());
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
