using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamCommandLineSplitter"/> against how a POSIX shell splits the command
/// <c>runtests.pl</c> hands it.
/// </summary>
[TestClass]
public sealed class UpstreamCommandLineSplitterTests
{
    public TestContext TestContext { get; set; } = null!;

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
    [DataRow("'|;&<>$`' \"|;&<>\"", new[] { "|;&<>$`", "|;&<>" })]
    [DataRow("", new string[0])]
    public void Split_SplitsAsTheShellDoes(string command, string[] expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("command", command);

        UpstreamCommandLine line = UpstreamCommandLineSplitter.Split(command);

        diagnostics.Act("arguments", string.Join(" | ", line.Arguments.Select(argument => $"[{argument}]")));
        diagnostics.Act("unsupported shell syntax", line.UnsupportedShellSyntax);
        diagnostics.Assert("arguments", string.Join(" | ", expected.Select(argument => $"[{argument}]")), string.Join(" | ", line.Arguments.Select(argument => $"[{argument}]")));
        diagnostics.Assert("unsupported shell syntax", null, line.UnsupportedShellSyntax);
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
    [DataRow("\"a $HOME\"", '$')]
    [DataRow("\"\\$a `id`\"", '`')]
    public void Split_ReportsTheFirstShellSyntaxAShellWouldActOn(string command, char expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("command", command);

        UpstreamCommandLine line = UpstreamCommandLineSplitter.Split(command);

        diagnostics.Act("unsupported shell syntax", line.UnsupportedShellSyntax);
        diagnostics.Assert("unsupported shell syntax", expected, line.UnsupportedShellSyntax);
        Assert.AreEqual(expected, line.UnsupportedShellSyntax);
    }

    [TestMethod]
    public void Split_DecodesEachArgumentAsUtf8()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string command = "Ã©";
        diagnostics.Arrange("command", command);

        UpstreamCommandLine line = UpstreamCommandLineSplitter.Split(command);

        diagnostics.Act("first argument", line.Arguments[0]);
        diagnostics.Diff("first argument", "é", line.Arguments[0]);
        Assert.AreEqual("é", line.Arguments[0]);
    }
}
