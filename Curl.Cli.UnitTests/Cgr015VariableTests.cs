using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-015 part 6 (BL-3848): curl 8.21.0's Windows build gets its arguments and environment in the
/// ANSI code page and expands <c>--variable</c> content as bytes, so <c>--variable v=é --expand-data {{v}}</c>
/// posts E9 under Windows-1252 while <c>--variable v@f.txt</c> with f.txt holding C3 A9 posts C3 A9;
/// measured with the local curl 8.21.0 (Schannel) on 2026-10-28 through <c>Record-CurlExchange.ps1</c>.
/// Off Windows the same commands send UTF-8. The Windows parse reads the encoding from
/// <see cref="CommandLineOptions.ConfigFileWireTextEncoding"/>, which is Windows-1252 on the measuring machine.
/// </summary>
[TestClass]
public sealed class Cgr015VariableTests
{
    private const string Url = "http://127.0.0.1/a";

    private static readonly byte[] Utf8EAcute = [0xC3, 0xA9];

    [TestMethod]
    public void Parse_TypedVariableOfEAcuteOnWindows_PostsTheAnsiCodePageByte()
    {
        CommandLineOptions options = Parse(["--variable", "v=é", "--expand-data", "{{v}}", Url], true, new RecordingDataFileReader());

        CollectionAssert.AreEqual(AnsiBytes(options, "é"), options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_FileVariableOfUtf8EAcuteOnWindows_PostsTheFileBytesUnchanged()
    {
        CommandLineOptions options = Parse(["--variable", "v@f.txt", "--expand-data", "{{v}}", Url], true, FileReader());

        CollectionAssert.AreEqual(Utf8EAcute, options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_TypedTextAroundAReferenceOnWindows_PostsTheAnsiCodePageBytes()
    {
        CommandLineOptions options = Parse(["--variable", "v=é", "--expand-data", "é{{v}}é", Url], true, new RecordingDataFileReader());

        CollectionAssert.AreEqual(AnsiBytes(options, "ééé"), options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_TypedVariableInExpandedUrlQueryOnWindows_EscapesTheAnsiCodePageByte()
    {
        CommandLineOptions options = Parse(["--variable", "v=é", "--expand-url-query", "q={{v}}", Url], true, new RecordingDataFileReader());

        Assert.AreEqual("q=%" + Convert.ToHexString(AnsiBytes(options, "é")), options.UrlQuery);
    }

    [TestMethod]
    public void Parse_FileVariableInExpandedHeaderOnWindows_SendsTypedTextInTheAnsiCodePageAndTheFileBytesUnchanged()
    {
        CommandLineOptions options = Parse(["--variable", "v@f.txt", "--expand-header", "X-A: é{{v}}", Url], true, FileReader());

        byte[] sent = options.ConfigFileWireTextEncoding!.GetBytes(options.Headers.Single());

        byte[] expected = [.. Encoding.ASCII.GetBytes("X-A: "), .. AnsiBytes(options, "é"), .. Utf8EAcute];
        CollectionAssert.AreEqual(expected, sent);
    }

    [TestMethod]
    public void Parse_TypedVariableOfEAcuteOffWindows_PostsUtf8()
    {
        CommandLineOptions options = Parse(["--variable", "v=é", "--expand-data", "{{v}}", Url], false, new RecordingDataFileReader());

        CollectionAssert.AreEqual(Utf8EAcute, options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_FileVariableOfUtf8EAcuteOffWindows_PostsTheFileBytesUnchanged()
    {
        CommandLineOptions options = Parse(["--variable", "v@f.txt", "--expand-data", "{{v}}", Url], false, FileReader());

        CollectionAssert.AreEqual(Utf8EAcute, options.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_FileVariableOfInvalidUtf8OffWindows_PostsTheFileBytesUnchanged()
    {
        RecordingDataFileReader reader = new();
        reader.Files["f.txt"] = [0xFF];

        CommandLineOptions options = Parse(["--variable", "v@f.txt", "--expand-data", "{{v}}", Url], false, reader);

        CollectionAssert.AreEqual(new byte[] { 0xFF }, options.PostData!.Value.ToArray());
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Parse_ImportedVariableOfEAcute_PostsItInTheArgumentEncoding(bool isWindows)
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--variable", "%V", "--expand-data", "{{V}}", Url], _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), new DefaultConfigFileSearch(_ => null, false, null, null), isWindows, name => name == "V" ? "é" : null);

        CommandLineOptions options = result.Options!;
        CollectionAssert.AreEqual(isWindows ? AnsiBytes(options, "é") : Utf8EAcute, options.PostData!.Value.ToArray());
    }

    private static RecordingDataFileReader FileReader()
    {
        RecordingDataFileReader reader = new();
        reader.Files["f.txt"] = Utf8EAcute;
        return reader;
    }

    private static byte[] AnsiBytes(CommandLineOptions options, string text)
    {
        byte[] expected = options.ConfigFileWireTextEncoding!.GetBytes(text);
        Assert.HasCount(text.Length, expected);
        return expected;
    }

    private static CommandLineOptions Parse(string[] arguments, bool isWindows, RecordingDataFileReader reader)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), reader, isWindows);
        Assert.IsNull(result.Refusal);
        return result.Options!;
    }
}
