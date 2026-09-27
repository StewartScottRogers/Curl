using System.Text;

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
        CommandLineParseResult result = CommandLineParser.Parse(arguments.Split(' '));

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.VersionRequested);
        Assert.IsEmpty(result.WarningLines);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_VersionAfterBundleValueLetter_EndsTheBundleBeforeTheValue()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-Vo", "x"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.Options.UrlOutputs);
    }

    [TestMethod]
    public void Parse_Version_StopsBeforeTheArgumentsAfterIt()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-V", "file:///nx"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.Options.Urls);
    }

    [TestMethod]
    public void Parse_WarningBeforeVersion_IsKept()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "-x", "-V"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.VersionRequested);
        CollectionAssert.AreEqual(new[] { "Warning: The filename argument '-x' looks like a flag." }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_RefusalBeforeVersion_IsRefused()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--bogus", "-V"]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual("curl: option --bogus: is unknown", result.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_UserWithoutPasswordThenVersion_NeverPrompts()
    {
        RecordingPasswordPrompt prompt = new();

        CommandLineParseResult result = CommandLineParser.Parse(["-u", "bob", "-V"], _ => false, prompt, new RecordingDataFileReader());

        Assert.IsTrue(result.Options!.VersionRequested);
        Assert.AreEqual(0, prompt.Calls);
    }

    [TestMethod]
    public void Parse_NoVersion_IsAcceptedAndDoesNotAskForTheVersion()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-version", "file:///nx"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.VersionRequested);
        CollectionAssert.AreEqual(new[] { "file:///nx" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_NoVersionOption_DoesNotAskForTheVersion()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["file:///nx"]);

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

        CommandLineParseResult withUrl = CommandLineParser.Parse(["-K", "vk.txt", "file:///nx"], _ => false, new RecordingPasswordPrompt(), reader);
        CommandLineParseResult alone = CommandLineParser.Parse(["-K", "vk.txt"], _ => false, new RecordingPasswordPrompt(), reader);

        Assert.IsTrue(withUrl.IsAccepted);
        Assert.IsFalse(withUrl.Options.VersionRequested);
        Assert.IsFalse(alone.IsAccepted);
        Assert.AreEqual("curl: (2) no URL specified", alone.Refusal.StandardErrorLines[0]);
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
