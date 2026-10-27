using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-052 (BL-3581): curl 8.22.0's Windows build sends the user name and password a UTF-8
/// <c>-K</c> file writes raw in a <c>url</c> as the file's own bytes (<c>Basic w6k64oKs</c> for
/// <c>url = "http://é:€@127.0.0.1:20581/a"</c>, measured), so the parsed URL's user information must
/// encode back into those bytes in the ANSI code page; the rest of the URL is left as read.
/// </summary>
[TestClass]
public sealed class Cgr052Tests
{
    [TestMethod]
    public void Parse_ConfigFileUrlWithUtf8UserInformationOnWindows_UserInformationEncodesToTheFileBytes()
    {
        CommandLineOptions options = ParseConfigFile("url = \"http://é:€@127.0.0.1/a\"\n", isWindows: true);

        string url = options.Urls.Single();
        Assert.EndsWith("@127.0.0.1/a", url);
        CollectionAssert.AreEqual(new byte[] { 0xC3, 0xA9, 0x3A, 0xE2, 0x82, 0xAC }, options.ConfigFileWireTextEncoding!.GetBytes(url["http://".Length..url.IndexOf('@')]));
    }

    [TestMethod]
    public void Parse_ConfigFileUrlWithoutSchemeOnWindows_RespellsTheUserInformation()
    {
        CommandLineOptions options = ParseConfigFile("url = \"é@h/a\"\n", isWindows: true);

        Assert.AreEqual("Ã©@h/a", options.Urls.Single());
    }

    [TestMethod]
    [DataRow("http://h/é@b")]
    [DataRow("http://h?é@b")]
    [DataRow("http://h#é@b")]
    [DataRow("http://éh")]
    public void Parse_ConfigFileUrlWithNoUserInformationOnWindows_KeepsTheUrl(string url)
    {
        CommandLineOptions options = ParseConfigFile("url = \"" + url + "\"\n", isWindows: true);

        Assert.AreEqual(url, options.Urls.Single());
    }

    [TestMethod]
    public void Parse_CommandLineUrlWithUserInformationOnWindows_KeepsTheUrl()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["http://é@h/"], _ => true, ConsolePasswordPrompt.ForProcessConsole, new RecordingDataFileReader(), true);

        Assert.AreEqual("http://é@h/", result.Options!.Urls.Single());
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
