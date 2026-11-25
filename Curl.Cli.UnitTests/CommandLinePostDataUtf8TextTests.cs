using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="CommandLineOptions.PostDataWithTypedTextAsUtf8"/> (BL-5915, ADR-1351): typed body text in UTF-8
/// on the Windows build too, every other byte as in <see cref="CommandLineOptions.PostData"/>.
/// </summary>
[TestClass]
public sealed class CommandLinePostDataUtf8TextTests
{
    // Not valid UTF-8, so a re-spelling of it would show.
    private static readonly byte[] Latin1File = [0x63, 0x61, 0x66, 0xE9];

    [TestMethod]
    [DataRow("-d")]
    [DataRow("--data-ascii")]
    [DataRow("--data-raw")]
    [DataRow("--data-binary")]
    [DataRow("--json")]
    public void Parse_TypedTextOnTheWindowsBuild_IsUtf8BesideTheCodePageBytes(string option)
    {
        CommandLineOptions options = Parse(isWindows: true, option, "café");

        AssertBytes(CodePageBytes(options, "café"), options.PostData);
        AssertBytes(Utf8("café"), options.PostDataWithTypedTextAsUtf8);
    }

    [TestMethod]
    public void Parse_TypedTextOffTheWindowsBuild_IsTheSameBytesTwice()
    {
        CommandLineOptions options = Parse(isWindows: false, "-d", "café", "--json", "é");

        AssertBytes(Utf8("café" + "é"), options.PostData);
        AssertBytes(Utf8("café" + "é"), options.PostDataWithTypedTextAsUtf8);
    }

    [TestMethod]
    public void Parse_FilesAroundTypedText_KeepTheirBytes()
    {
        CommandLineOptions options = Parse(
            isWindows: true, "--data-binary", "@latin1", "-d", "é", "-d", "@latin1", "--data-binary", "@latin1", "--json", "@latin1");

        byte[] file = Latin1File;
        AssertBytes([.. file, (byte)'&', .. CodePageBytes(options, "é"), (byte)'&', .. file, (byte)'&', .. file, .. file], options.PostData);
        AssertBytes([.. file, (byte)'&', .. Utf8("é"), (byte)'&', .. file, (byte)'&', .. file, .. file], options.PostDataWithTypedTextAsUtf8);
    }

    [TestMethod]
    public void Parse_UrlEncodedValue_SpellsTheNameInUtf8AndEscapesTheCodePageBytes()
    {
        CommandLineOptions options = Parse(isWindows: true, "--data-urlencode", "é=é");

        string escaped = UrlEncodedContent.Escape(CodePageBytes(options, "é"));
        AssertBytes(Utf8("é=" + escaped), options.PostDataWithTypedTextAsUtf8);
    }

    [TestMethod]
    public void Parse_ExpandedValue_KeepsTheBytesItGoesOutAs()
    {
        CommandLineOptions options = Parse(
            isWindows: true, "--variable", "name@latin1", "--expand-data", "é{{name}}", "-d", "é");

        byte[] expanded = [.. CodePageBytes(options, "é"), .. Latin1File, (byte)'&'];
        AssertBytes([.. expanded, .. CodePageBytes(options, "é")], options.PostData);
        AssertBytes([.. expanded, .. Utf8("é")], options.PostDataWithTypedTextAsUtf8);
    }

    [TestMethod]
    public void Parse_ExpandOptionThatReplacesNothing_IsTypedText()
    {
        CommandLineOptions options = Parse(isWindows: true, "--expand-data", "é");

        AssertBytes(Utf8("é"), options.PostDataWithTypedTextAsUtf8);
    }

    [TestMethod]
    public void Parse_ConfigFileLine_KeepsTheBytesItGoesOutAs()
    {
        CommandLineOptions options = Parse(isWindows: true, "-K", "config");

        AssertBytes([.. options.PostData!.Value.Span], options.PostDataWithTypedTextAsUtf8);
        AssertBytes(Latin1File, options.PostDataWithTypedTextAsUtf8);
    }

    [TestMethod]
    public void Parse_NoBody_HasNoUtf8BodyEither()
    {
        CommandLineOptions options = Parse(isWindows: true);

        Assert.IsNull(options.PostDataWithTypedTextAsUtf8);
    }

    private static CommandLineOptions Parse(bool isWindows, params string[] arguments)
    {
        var reader = new RecordingDataFileReader();
        reader.Files["latin1"] = Latin1File;
        reader.Files["config"] = [.. "data = "u8, .. Latin1File, (byte)'\n'];

        CommandLineParseResult result = CommandLineParser.Parse(
            [.. arguments, "xmpp://example.com/a@b"], _ => true, new EmptyAnswerPasswordPrompt(), reader, isWindows);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private static byte[] CodePageBytes(CommandLineOptions options, string text) =>
        options.ConfigFileWireTextEncoding!.GetBytes(text);

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static void AssertBytes(byte[] expected, ReadOnlyMemory<byte>? actual) =>
        CollectionAssert.AreEqual(expected, actual!.Value.ToArray());

    private sealed class EmptyAnswerPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => string.Empty;
    }
}
