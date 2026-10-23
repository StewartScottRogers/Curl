namespace Curl.Conformance.Differential.UnitTests;

[TestClass]
public sealed class CommandLineSplitterTests
{
    [TestMethod]
    public void Split_ReportCommandLine_SplitsAsShlexDid()
    {
        var arguments = CommandLineSplitter.Split("-sS --raw -H 'TE: trailers' {U}/chunked ");

        CollectionAssert.AreEqual(new[] { "-sS", "--raw", "-H", "TE: trailers", "{U}/chunked" }, arguments.ToArray());
    }

    [TestMethod]
    public void Split_EmptyQuotesAndBlanks_KeepTheEmptyArgument()
    {
        var arguments = CommandLineSplitter.Split("\t-b ''  -v\r\n");

        CollectionAssert.AreEqual(new[] { "-b", string.Empty, "-v" }, arguments.ToArray());
    }

    [TestMethod]
    public void Split_DoubleQuotes_UnescapeOnlyQuoteAndBackslash()
    {
        var arguments = CommandLineSplitter.Split(@"a""b \"" \\ \n""c");

        CollectionAssert.AreEqual(new[] { @"ab "" \ \nc" }, arguments.ToArray());
    }

    [TestMethod]
    public void Split_BackslashOutsideQuotes_KeepsTheNextCharacter()
    {
        var arguments = CommandLineSplitter.Split(@"a\ b \'");

        CollectionAssert.AreEqual(new[] { "a b", "'" }, arguments.ToArray());
    }

    [TestMethod]
    [DataRow("'open")]
    [DataRow("\"open")]
    [DataRow("\"open\\")]
    [DataRow("end\\")]
    public void Split_UnfinishedQuoteOrEscape_ThrowsFormatException(string commandLine)
    {
        Assert.ThrowsExactly<FormatException>(() => CommandLineSplitter.Split(commandLine));
    }
}
