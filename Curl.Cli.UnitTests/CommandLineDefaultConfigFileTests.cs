using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the default config file (<c>.curlrc</c>) is read before the command line and how
/// <c>-q</c> / <c>--disable</c> skips it. Measured with the local curl 8.21.0 (mingw, Schannel) on
/// 2026-09-27 in Git Bash, <c>CURL_HOME</c> pointing at a directory whose <c>.curlrc</c> held the lines
/// each test names and <c>XDG_CONFIG_HOME</c> unset; <c>-V</c> ended each run without a transfer, and
/// URLs were read back with <c>-s -w '[%{urlnum}%{url_effective}]'</c> against <c>http://127.0.0.1:1/</c>.
/// </summary>
[TestClass]
public sealed class CommandLineDefaultConfigFileTests
{
    private const string Home = @"C:\Users\Stewart Rogers\AppData\Local\Temp\rc\q";

    private const string Curlrc = Home + @"\.curlrc";

    private const string Url = "http://example.com/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    /// <summary>
    /// curl 8.21.0's two lines for the <c>bogus-QRC</c> line of <see cref="Curlrc"/>, the first ending in a space.
    /// </summary>
    private static readonly string[] BogusLineErrorLines =
    [
        @"curl: C:\Users\Stewart Rogers\AppData\Local\Temp\rc\q\.curlrc:1 config file ",
        "curl: option 'bogus-QRC' is unknown",
    ];

    [TestMethod]
    public void Parse_DefaultConfigFile_IsAppliedBeforeTheCommandLine()
    {
        // curl -s -w '[%{urlnum}%{url_effective}]' http://127.0.0.1:2/ printed [0http://127.0.0.1:1/][1http://127.0.0.1:2/].
        CommandLineParseResult result = Parse(["http://127.0.0.1:2/"], "url = http://127.0.0.1:1/\n");

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:1/", "http://127.0.0.1:2/" }, result.Options.Urls.ToArray());
        Assert.AreEqual(Curlrc, result.Options.DefaultConfigFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_EmptyCommandLineWithUrlInDefaultConfigFile_IsAccepted()
    {
        // curl (no arguments) with the file's URL tried it: exit 7, not the try-help line.
        CommandLineParseResult result = Parse([], "url = http://127.0.0.1:1/\n");

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:1/" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyCommandLineWithRefusedDefaultConfigFile_PrintsItsErrorThenTryHelp()
    {
        // curl (no arguments): the two error lines, then the try-help line; exit 2.
        CommandLineParseResult result = Parse([], "bogus-QRC\n");

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(BogusLineErrorLines, result.WarningLines.ToArray());
        CollectionAssert.AreEqual(new[] { TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyCommandLineWithNoDefaultConfigFile_IsRefusedWithTryHelpAlone()
    {
        CommandLineParseResult result = Parse([], new RecordingDataFileReader());

        CollectionAssert.AreEqual(new[] { TryHelp }, result.Refusal!.StandardErrorLines.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_RefusedDefaultConfigFileLine_StopsTheFileButNotTheCommandLine()
    {
        // curl -s -w ... with url, bogus-QRC and a second url: the two lines, then only the first URL tried; exit 7.
        CommandLineParseResult result = Parse(["-s"], "url = http://127.0.0.1:1/\nbogus-QRC\nurl http://second/\n");

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "http://127.0.0.1:1/" }, result.Options.Urls.ToArray());
        CollectionAssert.AreEqual(
            new[] { @"curl: C:\Users\Stewart Rogers\AppData\Local\Temp\rc\q\.curlrc:2 config file ", "curl: option 'bogus-QRC' is unknown" },
            result.WarningLines.ToArray());
        Assert.IsNull(result.Options.DefaultConfigFile);
    }

    [TestMethod]
    public void Parse_RefusedDefaultConfigFileLineAfterSilent_PrintsNothing()
    {
        // curl -V with "silent" then "bogus-QRC": nothing on standard error; exit 0.
        CommandLineParseResult result = Parse(["-V"], "silent\nbogus-QRC\n");

        Assert.IsTrue(result.Options!.VersionRequested);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_DefaultConfigFileLineReadingAMissingFile_AddsTheCannotReadLine()
    {
        // curl -V with "-K missing.cfg": these five lines; exit 0.
        CommandLineParseResult result = Parse([Url], "-K missing.cfg\n");

        CollectionAssert.AreEqual(
            new[]
            {
                "curl: cannot read config from 'missing.cfg'",
                @"curl: C:\Users\Stewart Rogers\AppData\Local\Temp\rc\q\.curlrc:1 config file ",
                "curl: option '-K' error encountered when reading a file",
                @"curl: cannot read config from 'C:\Users\Stewart ",
                @"curl: Rogers\AppData\Local\Temp\rc\q\.curlrc'",
            },
            result.WarningLines.ToArray());
        Assert.IsTrue(result.IsAccepted);
    }

    [TestMethod]
    public void Parse_DefaultConfigFileNestingConfigFiles_DoesNotCountAsALevel()
    {
        RecordingDataFileReader reader = new();
        reader.Files[Curlrc] = Encoding.UTF8.GetBytes("-K 1.cfg\n");
        for (int level = 1; level < 5; level++)
        {
            reader.Files[$"{level}.cfg"] = Encoding.UTF8.GetBytes($"-K {level + 1}.cfg\n");
        }

        reader.Files["5.cfg"] = Encoding.UTF8.GetBytes($"url {Url}\n");

        CommandLineParseResult result = Parse(["-s"], reader);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { Url }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    [DataRow("-q")]
    [DataRow("-qs")]
    [DataRow("-qV")]
    [DataRow("--disable")]
    public void Parse_FirstArgumentDisablingIt_SkipsTheDefaultConfigFile(string first)
    {
        // curl -q -V, -qV, -qs -V and --disable -V printed nothing on standard error.
        RecordingDataFileReader reader = new();
        reader.Files[Curlrc] = Encoding.UTF8.GetBytes("bogus-QRC\n");

        CommandLineParseResult result = Parse([first, "-V"], reader);

        Assert.IsTrue(result.Options!.VersionRequested);
        Assert.IsEmpty(result.WarningLines);
        Assert.IsEmpty(reader.Reads);
    }

    [TestMethod]
    [DataRow("--disable=x")]
    [DataRow("--no-disable")]
    [DataRow("-s", "-q")]
    [DataRow("-Vq")]
    [DataRow("-sq")]
    public void Parse_DisableAnywhereElse_ReadsTheDefaultConfigFile(params string[] before)
    {
        // curl --disable=x -V, --no-disable -V, -s -q -V, -Vq and -sq -V printed the file's two error lines.
        CommandLineParseResult result = Parse([.. before, "-V"], "bogus-QRC\n");

        Assert.IsTrue(result.Options!.VersionRequested);
        CollectionAssert.AreEqual(BogusLineErrorLines, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_DisableInDefaultConfigFile_IsIgnored()
    {
        // curl -V with "disable", "-q" and "bogus-QRC": the third line was still read and refused.
        CommandLineParseResult result = Parse(["-V"], "disable\n-q\nbogus-QRC\n");

        Assert.IsTrue(result.Options!.VersionRequested);
        CollectionAssert.AreEqual(
            new[] { @"curl: C:\Users\Stewart Rogers\AppData\Local\Temp\rc\q\.curlrc:3 config file ", "curl: option 'bogus-QRC' is unknown" },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_DefaultConfigFileReadToTheEnd_IsNamed()
    {
        CommandLineParseResult result = Parse(["-V"], $"disable\nurl {Url}\n");

        CollectionAssert.AreEqual(new[] { Url }, result.Options!.Urls.ToArray());
        Assert.AreEqual(Curlrc, result.Options.DefaultConfigFile);
    }

    [TestMethod]
    [DataRow("-q")]
    [DataRow("--disable")]
    [DataRow("--no-disable")]
    public void Parse_Disable_IsAcceptedAndChangesNothing(string argument)
    {
        // curl -q printed curl: (2) no URL specified; -q --no-disable -V printed nothing.
        CommandLineParseResult result = CommandLineParser.Parse([argument, "-q", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { Url }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_NullFirstArgument_ReadsTheDefaultConfigFile()
    {
        CommandLineParseResult result = Parse([null!, Url], "bogus-QRC\n");

        CollectionAssert.AreEqual(BogusLineErrorLines, result.WarningLines.ToArray());
        CollectionAssert.AreEqual(new[] { "curl: option : blank argument where content is expected", TryHelp }, result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_FirstReadableCandidate_IsTheFile()
    {
        RecordingDataFileReader reader = new();
        reader.Files[@"C:\home\_curlrc"] = Encoding.UTF8.GetBytes($"url {Url}\n");
        reader.Files[@"C:\up\.curlrc"] = Encoding.UTF8.GetBytes("url http://not-read/\n");
        DefaultConfigFileSearch search = new(
            name => name switch { "HOME" => @"C:\home", "USERPROFILE" => @"C:\up", _ => null },
            true,
            null,
            null);

        CommandLineParseResult result = CommandLineParser.Parse(["-s"], _ => true, new UnexpectedPasswordPrompt(), reader, search);

        CollectionAssert.AreEqual(new[] { Url }, result.Options!.Urls.ToArray());
        Assert.AreEqual(@"C:\home\_curlrc", result.Options.DefaultConfigFile);
        CollectionAssert.AreEqual(new[] { @"C:\home\.curlrc", @"C:\home\_curlrc" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_OverloadsWithoutASearch_NeverReadADefaultConfigFile()
    {
        RecordingDataFileReader reader = new();

        CommandLineParseResult result = CommandLineParser.Parse([Url], _ => true, new UnexpectedPasswordPrompt(), reader);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.DefaultConfigFile);
        Assert.IsEmpty(reader.Reads);
    }

    [TestMethod]
    public void Parse_NullSearch_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineParser.Parse([Url], _ => true, new UnexpectedPasswordPrompt(), new RecordingDataFileReader(), null!));
    }

    [TestMethod]
    [DataRow(new[] { "-v", Url })]
    [DataRow(new[] { "-v", "--bogus", Url })]
    [DataRow(new[] { "-v" })]
    public void Parse_VerboseReadWithDefaultConfigFile_NotesTheFile(string[] arguments)
    {
        // BL-352: curl -v --bogus and curl -v (no URL) print the note, as an accepted -v does.
        CommandLineParseResult result = Parse(arguments, "silent\n");

        Assert.AreEqual(Curlrc, result.NotedDefaultConfigFile);
    }

    [TestMethod]
    [DataRow(new[] { Url })]
    [DataRow(new[] { "--bogus", "-v", Url })]
    public void Parse_NoVerboseReadWithDefaultConfigFile_NotesNoFile(string[] arguments)
    {
        // BL-352: curl --bogus -v prints no note; the -v was never read.
        CommandLineParseResult result = Parse(arguments, "silent\n");

        Assert.IsNull(result.NotedDefaultConfigFile);
    }

    [TestMethod]
    public void Parse_EmptyCommandLineWithVerboseDefaultConfigFile_NotesNoFile()
    {
        // BL-352: curl (no arguments) with a .curlrc of verbose printed the try-help line alone.
        CommandLineParseResult result = Parse([], "verbose\n");

        Assert.IsNull(result.NotedDefaultConfigFile);
        Assert.IsFalse(result.Refusal!.FoundAtTransferSetup);
    }

    [TestMethod]
    public void Parse_VerboseWithNoUrl_IsRefusedAtTransferSetup()
    {
        CommandLineParseResult result = Parse(["-v"], "silent\n");

        Assert.IsTrue(result.Refusal!.FoundAtTransferSetup);
    }

    [TestMethod]
    public void Parse_UnknownOption_IsNotRefusedAtTransferSetup()
    {
        CommandLineParseResult result = Parse(["--bogus"], "silent\n");

        Assert.IsFalse(result.Refusal!.FoundAtTransferSetup);
    }

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, string? curlrc)
    {
        RecordingDataFileReader reader = new();
        if (curlrc is not null)
        {
            reader.Files[Curlrc] = Encoding.UTF8.GetBytes(curlrc);
        }

        return Parse(arguments, reader);
    }

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, RecordingDataFileReader reader)
    {
        DefaultConfigFileSearch search = new(name => name == "CURL_HOME" ? Home : null, true, null, null);
        return CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader, search);
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
