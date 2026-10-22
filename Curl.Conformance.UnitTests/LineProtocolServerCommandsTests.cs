using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="LineProtocolServerCommands"/>' <c>REPLY "&lt;COMMAND&gt; &lt;ARGUMENT&gt;" &lt;text&gt;</c> lines
/// to ftpserver.pl: the text is kept under the whole command line, apart from plain <c>REPLY</c> lines.
/// </summary>
[TestClass]
public sealed class LineProtocolServerCommandsTests
{
    [TestMethod]
    public void TryFindReply_ReplyLfLine_EndsTheTextInABareLineFeedAndIsNoKeywordLine()
    {
        // test891: "REPLYLF AUTH +" answers AUTH with "+\n"; it must not be read as an AUTH line.
        LineProtocolServerCommands commands = Read("AUTH CRAM-MD5\nREPLYLF AUTH +\n");

        Assert.IsTrue(commands.TryFindReply("AUTH", out byte[] reply));
        Assert.AreEqual("+\n", Encoding.Latin1.GetString(reply));
        CollectionAssert.AreEqual(new[] { "CRAM-MD5" }, commands.AuthenticationMechanisms.ToArray());
    }

    [TestMethod]
    public void TryFindFullTextReply_QuotedCommandAndArgument_IsFoundForThatFullTextOnly()
    {
        LineProtocolServerCommands commands = Read("REPLY \"AUTHENTICATE CRAM-MD5\" + Rubbish\n");

        Assert.IsTrue(commands.TryFindFullTextReply("AUTHENTICATE", "CRAM-MD5", out byte[] reply));
        Assert.AreEqual("+ Rubbish\r\n", Encoding.Latin1.GetString(reply));
        Assert.IsFalse(commands.TryFindFullTextReply("AUTHENTICATE", null, out _));
        Assert.IsFalse(commands.TryFindFullTextReply("AUTHENTICATE", string.Empty, out _));
        Assert.IsFalse(commands.TryFindFullTextReply("AUTHENTICATE", "PLAIN", out _));
        Assert.IsFalse(commands.TryFindReply("AUTHENTICATE", out _));
        Assert.IsFalse(commands.TryFindReply("\"AUTHENTICATE", out _));
    }

    [TestMethod]
    public void TryFindFullTextReply_NoArgument_IsFoundByTheCommandAlone()
    {
        LineProtocolServerCommands commands = Read("REPLY \"NOOP\" x\nREPLY \"STAT 1\" +OK one\\r\\n+OK two\n");

        Assert.IsFalse(commands.TryFindFullTextReply("NOOP", null, out _));
        Assert.IsTrue(commands.TryFindFullTextReply("STAT", "1", out byte[] reply));
        Assert.AreEqual("+OK one\r\n+OK two\r\n", Encoding.Latin1.GetString(reply));
    }

    [TestMethod]
    public void TryFindFullTextReply_CommandMatchedAsWritten_NotIgnoringCase()
    {
        LineProtocolServerCommands commands = Read("REPLY \"AUTH PLAIN\" +\n");

        Assert.IsTrue(commands.TryFindFullTextReply("AUTH", "PLAIN", out _));
        Assert.IsFalse(commands.TryFindFullTextReply("auth", "PLAIN", out _));
    }

    [TestMethod]
    public void Read_FullTextLine_IsNotAlsoCountedOrReadAsAKeywordLine()
    {
        LineProtocolServerCommands commands = Read("REPLY \"AUTH PLAIN\" AUTH LOGIN CAPA X\nAUTH CRAM-MD5\n");

        CollectionAssert.AreEqual(new[] { "CRAM-MD5" }, commands.AuthenticationMechanisms.ToArray());
        Assert.AreEqual(0, commands.Capabilities.Count);
    }

    [TestMethod]
    public void TryFindReply_HexEscape_IsTheCharacterWithThatCode()
    {
        // test2108: "REPLY PASS 230 logged\x00 in" answers PASS with a NUL byte, as Perl's qq reads \x00.
        LineProtocolServerCommands commands = Read("REPLY PASS 230 logged\\x00 in\nREPLY USER a\\x41\\x4ab\\xg\\x\n");

        Assert.IsTrue(commands.TryFindReply("PASS", out byte[] pass));
        Assert.AreEqual("230 logged\0 in\r\n", Encoding.Latin1.GetString(pass));
        Assert.IsTrue(commands.TryFindReply("USER", out byte[] user));
        Assert.AreEqual("aAJb\0g\0\r\n", Encoding.Latin1.GetString(user));
    }

    private static LineProtocolServerCommands Read(string serverCommands) =>
        LineProtocolServerCommands.Read(Encoding.Latin1.GetBytes(serverCommands));
}
