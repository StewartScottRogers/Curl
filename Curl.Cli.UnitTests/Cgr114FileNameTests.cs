using System.Text;

namespace Curl.Cli;

/// <summary>
/// CGR-114 (BL-3900, ADR-0843): curl 8.22.0's mingw build opens a UTF-8 config file's <c>output</c>
/// value as the file's bytes read in the ANSI code page, so on Windows the parser re-spells it there,
/// while an <c>-o</c> argument stays as the command line gave it and other platforms read UTF-8.
/// </summary>
[TestClass]
public sealed class Cgr114FileNameTests
{
    private const string Url = "http://127.0.0.1/bin";
    private static readonly byte[] Utf8Name = Encoding.UTF8.GetBytes("café.bin");

    [TestMethod]
    public void Parse_OutputInUtf8ConfigFileOnWindows_TakesTheFileBytesInTheAnsiCodePage()
    {
        CommandLineParseResult result = ParseConfigFile(isWindows: true);

        Encoding ansiCodePage = result.Options!.ConfigFileWireTextEncoding!;
        Assert.AreEqual(ansiCodePage.GetString(Utf8Name), result.Options.UrlOutputs[0].FileName);
        CollectionAssert.AreEqual(Utf8Name, ansiCodePage.GetBytes(result.Options.UrlOutputs[0].FileName!));
    }

    [TestMethod]
    public void Parse_OutputInUtf8ConfigFileOffWindows_KeepsTheUtf8Name()
    {
        CommandLineParseResult result = ParseConfigFile(isWindows: false);

        Assert.AreEqual("café.bin", result.Options!.UrlOutputs[0].FileName);
    }

    [TestMethod]
    public void Parse_OutputArgumentOnWindows_KeepsTheNameAsGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "café.bin", Url], _ => true, new RefusingPasswordPrompt(), new RecordingDataFileReader(), isWindows: true);

        Assert.AreEqual("café.bin", result.Options!.UrlOutputs[0].FileName);
    }

    private static CommandLineParseResult ParseConfigFile(bool isWindows)
    {
        RecordingDataFileReader reader = new();
        reader.Files["c.cfg"] = [.. "output = \""u8, .. Utf8Name, (byte)'"', (byte)'\n'];

        return CommandLineParser.Parse(["-K", "c.cfg", Url], _ => true, new RefusingPasswordPrompt(), reader, isWindows);
    }

    private sealed class RefusingPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
