using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-059 for a config file (BL-3956): curl 8.22.0's Windows build uses a UTF-8 <c>-K</c>
/// file's bytes raw, so a host written unescaped as <c>ü</c> reaches libidn2 as <c>C3 BC</c>, read as
/// Windows-1252 (<c>Ã¼</c>), and goes out as <c>Host: xn--14-ria7423a.example:20200</c> (measured
/// 2026-10-28), the answer <c>%C3%BC</c> gives. The parsed URL's host must therefore encode back
/// into the file's bytes in the ANSI code page; a host typed in an argument is left as typed.
/// </summary>
[TestClass]
public sealed class Cgr059ConfigFileHostTests
{
    [TestMethod]
    public void Parse_ConfigFileUrlWithUtf8HostOnWindows_HostEncodesToTheFileBytes()
    {
        CommandLineOptions options = ParseConfigFile("url = \"http://ü.example:20200/a\"\n", isWindows: true);

        string url = options.Urls.Single();
        Assert.AreEqual("http://Ã¼.example:20200/a", url);
        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xBC }, options.ConfigFileWireTextEncoding!.GetBytes(url["http://".Length..url.IndexOf('.')]));
    }

    [TestMethod]
    public void Parse_ConfigFileUrlWithUtf8HostAndPathOnWindows_LeavesThePath()
    {
        CommandLineOptions options = ParseConfigFile("url = \"http://ü/é\"\n", isWindows: true);

        Assert.AreEqual("http://Ã¼/é", options.Urls.Single());
    }

    [TestMethod]
    public void Parse_ConfigFileUrlWithUtf8HostOffWindows_KeepsTheUrl()
    {
        CommandLineOptions options = ParseConfigFile("url = \"http://ü.example/a\"\n", isWindows: false);

        Assert.AreEqual("http://ü.example/a", options.Urls.Single());
    }

    [TestMethod]
    public void Parse_CommandLineUrlWithNonAsciiHostOnWindows_KeepsTheUrl()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["http://ü.example/a"], _ => true, ConsolePasswordPrompt.ForProcessConsole, new RecordingDataFileReader(), true);

        Assert.AreEqual("http://ü.example/a", result.Options!.Urls.Single());
    }

    private static CommandLineOptions ParseConfigFile(string text, bool isWindows)
    {
        RecordingDataFileReader reader = new();
        reader.Files["k.txt"] = Encoding.UTF8.GetBytes(text);

        CommandLineParseResult result = CommandLineParser.Parse(["-K", "k.txt"], _ => true, ConsolePasswordPrompt.ForProcessConsole, reader, isWindows);
        Assert.IsNull(result.Refusal);
        return result.Options!;
    }
}
