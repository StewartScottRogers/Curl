namespace Curl.Cli;

/// <summary>
/// Pins CGR-016 (BL-3545) for the <c>-X</c> method, now the only command-line value whose
/// <see cref="InvalidUtf8ByteEscape"/> surrogates the parse turns into their bytes' characters: one at
/// the start of the value is turned too, and a U+DC80 to U+DCFF that ends a valid pair is kept.
/// </summary>
[TestClass]
public sealed class Cgr016Tests
{
    private const string Url = "http://127.0.0.1/a";

    [TestMethod]
    public void Parse_MethodStartingWithInvalidByte_GivesTheByteCharacter()
    {
        CommandLineOptions options = Parse(["-X", InvalidUtf8ByteEscape.Decode([0xFF]) + "T", Url]);

        Assert.AreEqual("ÿT", options.RequestMethod);
    }

    [TestMethod]
    public void Parse_MethodWithPairEndingInDcSurrogate_SendsThePairsUtf8Bytes()
    {
        const string Method = "G􏳿T";

        CommandLineOptions options = Parse(["-X", Method, Url]);

        Assert.AreEqual(System.Text.Encoding.Latin1.GetString(System.Text.Encoding.UTF8.GetBytes(Method)), options.RequestMethod);
    }

    private static CommandLineOptions Parse(string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, ConsolePasswordPrompt.ForProcessConsole, new RecordingDataFileReader(), false);
        Assert.IsNull(result.Refusal);
        return result.Options!;
    }
}
