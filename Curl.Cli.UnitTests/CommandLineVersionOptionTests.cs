using System.Text;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-V</c> / <c>--version</c> as curl 8.21.0 parses it, measured with the mingw build on
/// Windows on 2026-09-26: it ends parsing where it stands, even inside a bundle (<c>-Vo</c>,
/// <c>-V!</c>), so nothing after it is read or refused and no URL is needed; a refusal before it
/// still wins (<c>--bogus -V</c>); warnings before it are kept (<c>-o -x -V</c>); a value attached to
/// <c>--version=</c> is ignored; <c>--no-version</c> is accepted and does nothing; and a
/// <c>version</c> or <c>-V</c> line in a <c>-K</c> file is ignored (the file alone reports no URL).
/// </summary>
[TestClass]
public sealed class CommandLineVersionOptionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-V")]
    [DataRow("--version")]
    [DataRow("--version=x")]
    [DataRow("-V --bogus")]
    [DataRow("-V file:///nx")]
    [DataRow("-sV")]
    [DataRow("-Vs")]
    [DataRow("-Vo")]
    [DataRow("-V!")]
    [DataRow("-o x -o y -V")]
    [DataRow("-V -o -x")]
    public void Parse_Version_IsAcceptedAndAsksForTheVersion(string arguments)
    {
        CommandLineParseResult result = Parse(arguments.Split(' '));

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("version requested", true, Recorded(result)?.VersionRequested);
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach([]), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Diagnostics.Assert("warning lines after transfers", CommandLineParseDiagnostics.QuoteEach([]), CommandLineParseDiagnostics.QuoteEach(result.WarningLinesAfterTransfers));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.VersionRequested);
        Assert.IsEmpty(result.WarningLines);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_VersionAfterBundleValueLetter_EndsTheBundleBeforeTheValue()
    {
        CommandLineParseResult result = Parse(["-Vo", "x"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("url output count", 0, Recorded(result)?.UrlOutputs.Count);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.Options.UrlOutputs);
    }

    [TestMethod]
    public void Parse_Version_StopsBeforeTheArgumentsAfterIt()
    {
        CommandLineParseResult result = Parse(["-V", "file:///nx"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach([]), CommandLineParseDiagnostics.QuoteEach(Recorded(result)?.Urls ?? []));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.Options.Urls);
    }

    [TestMethod]
    public void Parse_WarningBeforeVersion_IsKept()
    {
        CommandLineParseResult result = Parse(["-o", "-x", "-V"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("version requested", true, Recorded(result)?.VersionRequested);
        Diagnostics.Assert(
            "warning lines",
            CommandLineParseDiagnostics.QuoteEach(["Warning: The filename argument '-x' looks like a flag."]),
            CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.VersionRequested);
        CollectionAssert.AreEqual(new[] { "Warning: The filename argument '-x' looks like a flag." }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_RefusalBeforeVersion_IsRefused()
    {
        CommandLineParseResult result = Parse(["--bogus", "-V"]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("first stderr line", "curl: option --bogus: is unknown", CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines[0]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --bogus: is unknown", result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_UserWithoutPasswordThenVersion_NeverPrompts()
    {
        RecordingPasswordPrompt prompt = new();

        CommandLineParseResult result = Parse(["-u", "bob", "-V"], prompt, new RecordingDataFileReader());
        Diagnostics.Act("password prompts", prompt.Calls);

        Diagnostics.Assert("version requested", true, Recorded(result)?.VersionRequested);
        Diagnostics.Assert("password prompts", 0, prompt.Calls);
        Assert.IsTrue(result.Options!.VersionRequested);
        Assert.AreEqual(0, prompt.Calls);
    }

    [TestMethod]
    public void Parse_NoVersion_IsAcceptedAndDoesNotAskForTheVersion()
    {
        CommandLineParseResult result = Parse(["--no-version", "file:///nx"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("version requested", false, Recorded(result)?.VersionRequested);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach(["file:///nx"]), CommandLineParseDiagnostics.QuoteEach(Recorded(result)?.Urls ?? []));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.VersionRequested);
        CollectionAssert.AreEqual(new[] { "file:///nx" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_NoVersionOption_DoesNotAskForTheVersion()
    {
        CommandLineParseResult result = Parse(["file:///nx"]);

        Diagnostics.Assert("version requested", false, Recorded(result)?.VersionRequested);
        Assert.IsFalse(result.Options!.VersionRequested);
    }

    [TestMethod]
    [DataRow("version")]
    [DataRow("-V")]
    [DataRow("--version")]
    public void Parse_VersionInConfigFile_IsIgnored(string line)
    {
        RecordingDataFileReader reader = new();
        reader.Files["vk.txt"] = Encoding.UTF8.GetBytes(line + "\n");
        Diagnostics.Bytes("vk.txt", reader.Files["vk.txt"]);

        CommandLineParseResult withUrl = Parse(["-K", "vk.txt", "file:///nx"], new RecordingPasswordPrompt(), reader);
        CommandLineParseResult alone = Parse(["-K", "vk.txt"], new RecordingPasswordPrompt(), reader);

        Diagnostics.Assert("with url accepted", true, withUrl.IsAccepted);
        Diagnostics.Assert("with url version requested", false, Recorded(withUrl)?.VersionRequested);
        Diagnostics.Assert("alone accepted", false, alone.IsAccepted);
        Diagnostics.Assert("alone first stderr line", "curl: (2) no URL specified", CommandLineParseDiagnostics.Peek(alone.Refusal)?.StandardErrorLines[0]);
        Assert.IsTrue(withUrl.IsAccepted);
        Assert.IsFalse(withUrl.Options.VersionRequested);
        Assert.IsFalse(alone.IsAccepted);
        Assert.AreEqual("curl: (2) no URL specified", alone.Refusal.StandardErrorLines[0]);
    }

    /// <summary>
    /// Returns the parsed options, or null for a refusal, for diagnostic lines written before the test asserts
    /// acceptance, without making the compiler treat <see cref="CommandLineParseResult.Options"/> as possibly null.
    /// </summary>
    private static CommandLineOptions? Recorded(CommandLineParseResult result) => result.Options;

    /// <summary>Parses <paramref name="arguments"/>, writing them, the outcome and whether the version was asked for as diagnostics.</summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        return ActParse(CommandLineParser.Parse(arguments));
    }

    /// <summary>Parses <paramref name="arguments"/> with a password prompt and file reader, writing the same diagnostics.</summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, IPasswordPrompt prompt, RecordingDataFileReader reader)
    {
        Diagnostics.ArrangeArguments(arguments);
        return ActParse(CommandLineParser.Parse(arguments, _ => false, prompt, reader));
    }

    private CommandLineParseResult ActParse(CommandLineParseResult result)
    {
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("version requested", result.Options.VersionRequested);
            Diagnostics.Act("urls", CommandLineParseDiagnostics.QuoteEach(result.Options.Urls));
        }

        return result;
    }

    private sealed class RecordingPasswordPrompt : IPasswordPrompt
    {
        public int Calls { get; private set; }

        public string ReadPassword(string prompt)
        {
            Calls++;
            return string.Empty;
        }
    }
}
