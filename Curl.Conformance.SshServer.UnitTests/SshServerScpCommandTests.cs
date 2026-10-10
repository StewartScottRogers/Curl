namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerScpCommandTests
{
    [TestMethod]
    [DataRow("scp -pf '/x/it'\"'\"'s'\\!\"''\"'here'", true, "/x/it's!''here")]
    [DataRow("scp -t '/home/u/new.txt'", false, "/home/u/new.txt")]
    [DataRow("scp -p -f /plain", true, "/plain")]
    [DataRow("scp  -t  \"/a \\\"b\\\\ \\c\"", false, "/a \"b\\ \\c")]
    public void Parse_ScpCommand_ReadsDirectionAndUnquotedPath(string command, bool isSource, string path)
    {
        SshServerScpCommand? parsed = SshServerScpCommand.Parse(command);

        Assert.AreEqual(new SshServerScpCommand(isSource, path), parsed);
    }

    [TestMethod]
    [DataRow("cat /file")]
    [DataRow("scp /file")]
    [DataRow("scp -r /file")]
    [DataRow("scp -f")]
    public void Parse_NotAnScpTransfer_IsNull(string command)
    {
        SshServerScpCommand? parsed = SshServerScpCommand.Parse(command);

        Assert.IsNull(parsed);
    }

    [TestMethod]
    [DataRow("a 'b c", new[] { "a", "b c" })]
    [DataRow("a \"b", new[] { "a", "b" })]
    [DataRow("a b\\", new[] { "a", "b\\" })]
    [DataRow("a \\'b ", new[] { "a", "'b" })]
    [DataRow("'' x", new[] { "", "x" })]
    [DataRow("\"\\x\"", new[] { "\\x" })]
    [DataRow("\"x\\", new[] { "x\\" })]
    public void Words_QuotesAndEscapes_SplitAsTheShellDoes(string command, string[] expected)
    {
        List<string> words = SshServerScpCommand.Words(command);

        CollectionAssert.AreEqual(expected, words);
    }
}
