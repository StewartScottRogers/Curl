using System.Text;

namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Pins <see cref="ScpCommand" /> to libssh2 1.11.1's <c>shell_quotearg</c>, and to the
/// command curl 8.21.0 sent, as OpenSSH logged it 2026-09-29 (BL-574).
/// </summary>
[TestClass]
public sealed class ScpCommandTests
{
    [TestMethod]
    [DataRow("/x/it's!''here", "scp -pf '/x/it'\"'\"'s'\\!\"''\"'here'", DisplayName = "apostrophes and an exclamation mark, as measured")]
    [DataRow("/home/u/f", "scp -pf '/home/u/f'", DisplayName = "plain path, as measured")]
    [DataRow("a b.txt", "scp -pf 'a b.txt'", DisplayName = "space, as measured")]
    public void ForDownload_Path_BuildsTheCommandCurlSent(string path, string command)
    {
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(command), ScpCommand.ForDownload(Encoding.Latin1.GetBytes(path)));
    }

    [TestMethod]
    [DataRow("", "", DisplayName = "empty")]
    [DataRow("'", "\"'\"", DisplayName = "apostrophe alone")]
    [DataRow("!", "\\!", DisplayName = "exclamation mark alone")]
    [DataRow("!!", "\\!\\!", DisplayName = "two exclamation marks")]
    [DataRow("a'''b", "'a'\"'''\"'b'", DisplayName = "run of apostrophes, libssh2's example")]
    [DataRow("a!b", "'a'\\!'b'", DisplayName = "exclamation mark inside, libssh2's example")]
    [DataRow("'!", "\"'\"\\!", DisplayName = "apostrophe then exclamation mark")]
    [DataRow("!'", "\\!\"'\"", DisplayName = "exclamation mark then apostrophe")]
    [DataRow("!a", "\\!'a'", DisplayName = "exclamation mark then a letter")]
    public void Quote_Argument_QuotesItAsLibssh2Does(string argument, string quoted)
    {
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(quoted), ScpCommand.Quote(Encoding.Latin1.GetBytes(argument)));
    }
}
