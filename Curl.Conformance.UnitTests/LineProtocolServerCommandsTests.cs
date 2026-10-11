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

    private static LineProtocolServerCommands Read(string serverCommands) =>
        LineProtocolServerCommands.Read(Encoding.Latin1.GetBytes(serverCommands));
}
