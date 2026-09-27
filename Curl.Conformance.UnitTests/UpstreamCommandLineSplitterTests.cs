namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamCommandLineSplitter"/> against how a POSIX shell splits the command
/// <c>runtests.pl</c> hands it.
/// </summary>
[TestClass]
public sealed class UpstreamCommandLineSplitterTests
{
    [TestMethod]
    [DataRow("a b\tc", new[] { "a", "b", "c" })]
    [DataRow("  a \n b\r\n", new[] { "a", "b" })]
    [DataRow("'a b' \"c d\"", new[] { "a b", "c d" })]
    [DataRow("x'a'\"b\"y", new[] { "xaby" })]
    [DataRow("'' \"\"", new[] { "", "" })]
    [DataRow("'a\\\"b'", new[] { "a\\\"b" })]
    [DataRow("\"a\\\"b\\\\c\\$d\\`e\\nf\"", new[] { "a\"b\\c$d`e\\nf" })]
    [DataRow("a\\ b\\'c", new[] { "a b'c" })]
    [DataRow("a\\", new[] { "a" })]
    [DataRow("\"a\\", new[] { "a\\" })]
    [DataRow("'open", new[] { "open" })]
    [DataRow("'|;&<>$`' \"|;&<>`\"", new[] { "|;&<>$`", "|;&<>`" })]
    [DataRow("", new string[0])]
    public void Split_SplitsAsTheShellDoes(string command, string[] expected)
    {
        UpstreamCommandLine line = UpstreamCommandLineSplitter.Split(command);

        CollectionAssert.AreEqual(expected, line.Arguments.ToArray());
        Assert.IsNull(line.UnsupportedShellSyntax);
    }

    [TestMethod]
    [DataRow("a | b", '|')]
    [DataRow("a;b", ';')]
    [DataRow("a & b", '&')]
    [DataRow("a < b", '<')]
    [DataRow("a > b > c", '>')]
    [DataRow("$HOME", '$')]
    [DataRow("`id` |", '`')]
    public void Split_ReportsTheFirstUnquotedShellSyntax(string command, char expected)
    {
        UpstreamCommandLine line = UpstreamCommandLineSplitter.Split(command);

        Assert.AreEqual(expected, line.UnsupportedShellSyntax);
    }

    [TestMethod]
    public void Split_DecodesEachArgumentAsUtf8()
    {
        UpstreamCommandLine line = UpstreamCommandLineSplitter.Split("Ã©");

        Assert.AreEqual("é", line.Arguments[0]);
    }
}
