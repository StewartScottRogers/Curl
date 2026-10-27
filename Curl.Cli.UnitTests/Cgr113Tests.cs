using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-113 (BL-3642): curl 8.22.0's Windows build reads a <c>-K</c> file, <c>-K -</c> and
/// <c>.curlrc</c> in text mode, so a Ctrl-Z (0x1A) byte ends the file; the Linux build reads it as
/// content. Measured on Windows with the local curl 8.22.0 on 2026-10-26: <c>write-out = "A"</c> CR LF
/// Ctrl-Z <c> bogus = 1</c> writes <c>A</c> with nothing on stderr, and <c>write-out = "A</c> Ctrl-Z
/// <c>Z"</c> writes <c>A</c>.
/// </summary>
[TestClass]
public sealed class Cgr113Tests
{
    private const string Url = "http://example.com/";

    private const string BeforeAndAfterCtrlZ = "header = \"X-A: 1\"\r\n\x1A header = \"X-C: 3\"\r\n";

    [TestMethod]
    public void Parse_CtrlZInAConfigFileOnWindows_EndsTheFile()
    {
        CommandLineParseResult result = ParseConfigFile(BeforeAndAfterCtrlZ, isWindows: true);

        Assert.IsNull(result.Refusal);
        CollectionAssert.AreEqual(new[] { "X-A: 1" }, result.Options!.Headers.ToArray());
    }

    [TestMethod]
    public void Parse_CtrlZOnStandardInputOnWindows_EndsTheFile()
    {
        RecordingDataFileReader reader = new() { StandardInput = Encoding.Latin1.GetBytes(BeforeAndAfterCtrlZ) };

        CommandLineParseResult result = CommandLineParser.Parse(["-K", "-", Url], _ => true, new EmptyPasswordPrompt(), reader, true);

        Assert.IsNull(result.Refusal);
        CollectionAssert.AreEqual(new[] { "X-A: 1" }, result.Options!.Headers.ToArray());
    }

    [TestMethod]
    public void Parse_CtrlZInsideAQuotedParameterOnWindows_EndsTheParameter()
    {
        CommandLineParseResult result = ParseConfigFile("write-out = \"A\x1AZ\"\n", isWindows: true);

        Assert.AreEqual("A", result.Options!.WriteOut);
    }

    [TestMethod]
    public void Parse_UrlBeforeCtrlZOnWindows_IsTheOnlyUrl()
    {
        RecordingDataFileReader reader = new();
        reader.Files["k.cfg"] = Encoding.Latin1.GetBytes("url = http://127.0.0.1/nb03\n\x1A\nheader = X-After-Ctrl-Z:1\n");

        CommandLineParseResult result = CommandLineParser.Parse(["-sS", "-K", "k.cfg"], _ => true, new EmptyPasswordPrompt(), reader, true);

        CollectionAssert.AreEqual(new[] { "http://127.0.0.1/nb03" }, result.Options!.Urls.ToArray());
        Assert.IsEmpty(result.Options.Headers);
    }

    [TestMethod]
    public void Parse_CtrlZInAConfigFileOffWindows_IsContent()
    {
        CommandLineParseResult result = ParseConfigFile(BeforeAndAfterCtrlZ, isWindows: false);

        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
    }

    [TestMethod]
    public void Parse_CtrlZInTheCurlrcOnWindows_EndsTheFileWithNoWarning()
    {
        DefaultConfigFileSearch search = new(name => name == "CURL_HOME" ? "home" : null, true, null, null);
        RecordingDataFileReader reader = new();
        reader.Files[search.CandidatePaths()[0]] = Encoding.Latin1.GetBytes("header = X-Before:1\n\x1Aheader = X-After:1\n");

        CommandLineParseResult result = CommandLineParser.Parse(["-sS", Url], _ => true, new EmptyPasswordPrompt(), reader, search, true);

        CollectionAssert.AreEqual(new[] { "X-Before:1" }, result.Options!.Headers.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    private static CommandLineParseResult ParseConfigFile(string contents, bool isWindows)
    {
        RecordingDataFileReader reader = new();
        reader.Files["c.cfg"] = Encoding.Latin1.GetBytes(contents);
        return CommandLineParser.Parse(["-s", "-K", "c.cfg", Url], _ => true, new EmptyPasswordPrompt(), reader, isWindows);
    }
}
