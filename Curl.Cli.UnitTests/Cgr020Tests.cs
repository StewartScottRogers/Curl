using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-020 (BL-3549): curl 8.22.0 sends the bytes of a header file (<c>-H @file</c>,
/// <c>--proxy-header @file</c>, <c>-F ...;headers=@file</c>) and of a <c>-K</c> file's
/// <c>cookie</c> and <c>request</c> values unchanged, so the parsed text must encode back into
/// those bytes on the request side: in the ANSI code page on Windows, in UTF-8 with each unreadable
/// byte escaped elsewhere, and one character per byte for the cookie and the method.
/// </summary>
[TestClass]
public sealed class Cgr020Tests
{
    private const string Url = "http://127.0.0.1/a";

    private static readonly byte[] Utf8Header = [0x58, 0x3A, 0x20, 0xC3, 0xA9, 0xE4, 0xB8, 0xAD, 0x0A];

    [TestMethod]
    public void Parse_HeaderFileOfUtf8OnWindows_HeaderEncodesInTheAnsiCodePageToTheFileBytes()
    {
        CommandLineOptions options = Parse(["-H", "@h.txt", Url], true, ("h.txt", Utf8Header));

        CollectionAssert.AreEqual(Utf8Header[..^1], options.ConfigFileWireTextEncoding!.GetBytes(options.Headers.Single()));
    }

    [TestMethod]
    public void Parse_ProxyHeaderFileOfUtf8OnWindows_HeaderEncodesInTheAnsiCodePageToTheFileBytes()
    {
        CommandLineOptions options = Parse(["--proxy-header", "@h.txt", Url], true, ("h.txt", Utf8Header));

        CollectionAssert.AreEqual(Utf8Header[..^1], options.ConfigFileWireTextEncoding!.GetBytes(options.ProxyHeaders.Single()));
    }

    [TestMethod]
    public void Parse_FormHeaderFileOfUtf8OnWindows_PartHeaderEncodesInTheAnsiCodePageToTheFileBytes()
    {
        CommandLineOptions options = Parse(["-F", "a=b;headers=@h.txt", Url], true, ("h.txt", Utf8Header));

        CollectionAssert.AreEqual(Utf8Header[..^1], options.ConfigFileWireTextEncoding!.GetBytes(options.FormParts.Single().Headers.Single()));
    }

    [TestMethod]
    public void Parse_HeaderFileOfLatin1OffWindows_EscapesEachByteUtf8CannotRead()
    {
        CommandLineOptions options = Parse(["-H", "@h.txt", Url], false, ("h.txt", [0x58, 0x3A, 0x20, 0xE9, 0xFF, 0x0A]));

        Assert.AreEqual("X: \uDCE9\uDCFF", options.Headers.Single());
    }

    [TestMethod]
    public void Parse_FormHeaderFileOfMixedBytesOffWindows_ReadsUtf8AndEscapesTheRest()
    {
        CommandLineOptions options = Parse(["-F", "a=b;headers=@h.txt", Url], false, ("h.txt", [0x58, 0x2D, 0x43, 0x3A, 0x20, 0xFF, 0x80, 0xC3, 0x28, 0xC3, 0xA9, 0x0A]));

        Assert.AreEqual("X-C: \uDCFF\uDC80\uDCC3(é", options.FormParts.Single().Headers.Single());
    }

    [TestMethod]
    public void Parse_ConfigFileCookieOfUtf8_CookieHoldsTheFileBytesOneCharacterEach()
    {
        CommandLineOptions options = Parse(["-K", "k.txt", Url], false, ("k.txt", Encoding.UTF8.GetBytes("cookie = \"c=é\"\n")));

        Assert.AreEqual("c=Ã©", options.Cookies.Single().Value);
    }

    [TestMethod]
    public void Parse_ConfigFileRequestOfUtf8_MethodHoldsTheFileBytesOneCharacterEach()
    {
        CommandLineOptions options = Parse(["-K", "k.txt", Url], true, ("k.txt", Encoding.UTF8.GetBytes("request = \"é\"\n")));

        Assert.AreEqual("Ã©", options.RequestMethod);
    }

    [TestMethod]
    public void Parse_ConfigFileCookieInTheAnsiCodePageOnWindows_CookieHoldsTheFileBytesOneCharacterEach()
    {
        CommandLineOptions options = Parse(["-K", "k.txt", Url], true, ("k.txt", [.. Encoding.ASCII.GetBytes("cookie = \"c="), 0xE9, .. Encoding.ASCII.GetBytes("\"\n")]));

        Assert.AreEqual("c=é", options.Cookies.Single().Value);
    }

    [TestMethod]
    public void Parse_ConfigFileCookieFileName_KeepsTheNameAsRead()
    {
        CommandLineOptions options = Parse(["-K", "k.txt", Url], false, ("k.txt", Encoding.UTF8.GetBytes("cookie = \"jé.txt\"\n")));

        Assert.AreEqual("jé.txt", options.Cookies.Single().Value);
    }

    [TestMethod]
    public void Parse_CommandLineCookieAndRequest_KeepsTheCookieAndSendsTheMethodAsUtf8Bytes()
    {
        CommandLineOptions options = Parse(["-b", "c=é", "-X", "é", Url], false);

        Assert.AreEqual("c=é", options.Cookies.Single().Value);
        Assert.AreEqual("é", options.RequestMethod);
    }

    [TestMethod]
    public void Read_FileTheWireEncodingCannotCarry_ReadsUtf8EscapingTheRest()
    {
        string text = HeaderFileText.Read([0x61, 0xE9], Encoding.ASCII);

        Assert.AreEqual("a\uDCE9", text);
    }

    [TestMethod]
    public void Read_NullContents_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => HeaderFileText.Read(null!, null));

    private static CommandLineOptions Parse(string[] arguments, bool isWindows, params (string Path, byte[] Contents)[] files)
    {
        RecordingDataFileReader reader = new();
        foreach ((string path, byte[] contents) in files)
        {
            reader.Files[path] = contents;
        }

        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), reader, isWindows);
        Assert.IsNull(result.Refusal);
        return result.Options!;
    }
}
