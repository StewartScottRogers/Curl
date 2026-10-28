namespace Curl.Cli;

/// <summary>
/// Pins CGR-131 (BL-3660): curl 8.22.0's <c>-r</c> appends a dash to a leading number that
/// fits a <c>curl_off_t</c> whenever the next character is not <c>-</c>, whatever follows,
/// so <c>0 - 9</c> and <c>0x10-0x20</c> read as <c>0-</c>. Measured with the local curl 8.22.0
/// (Schannel) on 2026-10-26 over <c>file://</c> on a 292-byte file.
/// </summary>
[TestClass]
public sealed class Cgr131Tests
{
    private const string Url = "file:///dir/digits.txt";

    private const string MissingDashWarning = "Warning: A specified range MUST include at least one dash (-). Appending one for you";

    private const string InvalidCharacterWarning = "Warning: Invalid character is found in given range. A specified range MUST have only digits in 'start'-'stop'. The server's response to this request is uncertain.";

    [TestMethod]
    [DataRow("0 - 9", "0-", DisplayName = "0 - 9")]
    [DataRow("0x10-0x20", "0-", DisplayName = "0x10-0x20")]
    [DataRow("0 -9", "0-", DisplayName = "0 -9")]
    [DataRow("5 ", "5-", DisplayName = "5 and a blank")]
    public void Parse_LeadingNumberNotBeforeADash_AppendsADashAndWarns(string value, string expected)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-r", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.Range);
        CollectionAssert.AreEqual(new[] { MissingDashWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("0-9x", DisplayName = "0-9x")]
    [DataRow("99999999999999999999 - 3", DisplayName = "an overflowing first number")]
    [DataRow("-5 x", DisplayName = "-5 x")]
    public void Parse_NoDashToAppendAndAnInvalidCharacter_KeepsTheTextAndWarns(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-r", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(value, result.Options.Range);
        CollectionAssert.AreEqual(new[] { InvalidCharacterWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("1-2-3", DisplayName = "1-2-3")]
    [DataRow("0-1,5-6", DisplayName = "0-1,5-6")]
    [DataRow("0-0,0-0", DisplayName = "0-0,0-0")]
    public void Parse_NumberBeforeADash_KeepsTheTextWithoutAWarning(string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-r", value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(value, result.Options.Range);
        Assert.IsEmpty(result.WarningLines);
    }
}
