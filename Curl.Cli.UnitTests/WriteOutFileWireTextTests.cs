using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins how <c>-w</c> keeps file bytes apart from command-line text (CGR-015 part 4, BL-3841,
/// ADR-0828): on Windows the renderer writes <c>-w</c> in the ANSI code page, so a <c>-w @file</c>
/// template, and a <c>-w</c> in a UTF-8 config file, are re-spelled in that code page so their UTF-8
/// bytes come out unchanged, as real curl writes file bytes raw; an argument stays as typed.
/// </summary>
[TestClass]
public sealed class WriteOutFileWireTextTests
{
    private const string Url = "file:///nonexist";
    private static readonly byte[] Utf8Template = Encoding.UTF8.GetBytes("[é]");

    [TestMethod]
    public void Parse_WriteOutFileOnWindows_EncodesBackToTheFileBytes()
    {
        RecordingDataFileReader reader = new();
        reader.Files["wo.txt"] = Utf8Template;

        CommandLineParseResult result = CommandLineParser.Parse(["-w", "@wo.txt", Url], _ => true, new UnexpectedPasswordPrompt(), reader, isWindows: true);

        CollectionAssert.AreEqual(Utf8Template, result.Options!.ConfigFileWireTextEncoding!.GetBytes(result.Options.WriteOut!));
    }

    [TestMethod]
    public void Parse_WriteOutArgumentOnWindows_KeepsTheTextAsTyped()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-w", "[é]", Url], _ => true, new UnexpectedPasswordPrompt(), new RecordingDataFileReader(), isWindows: true);

        Assert.AreEqual("[é]", result.Options!.WriteOut);
    }

    [TestMethod]
    public void Parse_WriteOutInUtf8ConfigFileOnWindows_EncodesBackToTheFileBytes()
    {
        RecordingDataFileReader reader = new();
        reader.Files["config.txt"] = [.. "-w \""u8, .. Utf8Template, (byte)'"', (byte)'\n'];

        CommandLineParseResult result = CommandLineParser.Parse(["-K", "config.txt", Url], _ => true, new UnexpectedPasswordPrompt(), reader, isWindows: true);

        CollectionAssert.AreEqual(Utf8Template, result.Options!.ConfigFileWireTextEncoding!.GetBytes(result.Options.WriteOut!));
    }

    [TestMethod]
    public void Parse_WriteOutFileOffWindows_ReadsTheFileAsUtf8()
    {
        RecordingDataFileReader reader = new();
        reader.Files["wo.txt"] = Utf8Template;

        CommandLineParseResult result = CommandLineParser.Parse(["-w", "@wo.txt", Url], _ => true, new UnexpectedPasswordPrompt(), reader, isWindows: false);

        Assert.IsNull(result.Options!.ConfigFileWireTextEncoding);
        Assert.AreEqual("[é]", result.Options.WriteOut);
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
