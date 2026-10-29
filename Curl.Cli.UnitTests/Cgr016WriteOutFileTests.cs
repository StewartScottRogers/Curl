using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-016 part 6 (BL-3862): curl 8.18.0 on Linux and the Schannel curl 8.22.0 on Windows both
/// write <c>-w @fmt</c>, where <c>fmt</c> holds the bytes 41 FE 42, as 41 FE 42. Off Windows the parse
/// keeps the byte that is not valid UTF-8 as its <see cref="InvalidUtf8ByteEscape"/> surrogate, which the
/// write-out renderer writes as the byte (BL-3852), rather than U+FFFD, which it would write as EF BF BD;
/// on Windows it spells the bytes in the ANSI code page the renderer writes text in.
/// </summary>
[TestClass]
public sealed class Cgr016WriteOutFileTests
{
    private const string Url = "http://127.0.0.1/a";

    private static readonly byte[] TemplateBytes = [0x41, 0xFE, 0x42];

    [TestMethod]
    public void Parse_WriteOutFileWithInvalidByteOffWindows_KeepsTheByteAsItsEscape()
    {
        CommandLineOptions options = Parse(isWindows: false);

        Assert.AreEqual("A\uDCFEB", options.WriteOut);
    }

    [TestMethod]
    public void Parse_WriteOutFileWithInvalidByteOnWindows_GivesTheBytesBackInTheWireEncoding()
    {
        CommandLineOptions options = Parse(isWindows: true);

        CollectionAssert.AreEqual(TemplateBytes, options.ConfigFileWireTextEncoding!.GetBytes(options.WriteOut!));
    }

    [TestMethod]
    public void WriteOutFileTemplate_EndingInALoneLeadByteTheWireEncodingCannotCarry_KeepsTheInvalidByteAsItsEscape()
    {
        Encoding shiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932)!;

        Assert.AreEqual("A\uDCFEB\uDC81", CommandLineOptionTable.WriteOutFileTemplate([0x41, 0xFE, 0x42, 0x81], shiftJis));
    }

    [TestMethod]
    public void WriteOutFileTemplate_NullBytes_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineOptionTable.WriteOutFileTemplate(null!, null));

    private static CommandLineOptions Parse(bool isWindows)
    {
        RecordingDataFileReader reader = new();
        reader.Files["fmt"] = TemplateBytes;
        return CommandLineParser.Parse(["-w", "@fmt", Url], _ => true, ConsolePasswordPrompt.ForProcessConsole, reader, isWindows).Options!;
    }
}
