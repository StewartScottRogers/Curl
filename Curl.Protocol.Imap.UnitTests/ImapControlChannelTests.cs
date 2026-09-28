using System.Text;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins the command tags curl 8.21.0 sends: <c>A</c> and a three-digit number counted in
/// an unsigned char, so <c>A255</c> is followed by <c>A000</c> (<c>lib/imap.c</c>,
/// <c>imap_sendf</c>).
/// </summary>
[TestClass]
public sealed class ImapControlChannelTests
{
    [TestMethod]
    public void Tag_BeforeAnyCommand_IsTheGreetingsStar()
    {
        var channel = new ImapControlChannel(new ScriptedConnection(), CancellationToken.None);

        Assert.AreEqual("*", channel.Tag);
    }

    [TestMethod]
    public async Task SendCommandAsync_TagsCountFromA001AndWrapAfterA255()
    {
        var connection = new ScriptedConnection();
        var channel = new ImapControlChannel(connection, CancellationToken.None);

        for (int command = 0; command < 257; command++)
        {
            await channel.SendCommandAsync("NOOP");
        }

        string[] lines = Encoding.Latin1.GetString(connection.Sent).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual("A001 NOOP", lines[0]);
        Assert.AreEqual("A255 NOOP", lines[254]);
        Assert.AreEqual("A000 NOOP", lines[255]);
        Assert.AreEqual("A001 NOOP", lines[256]);
        Assert.AreEqual("A001", channel.Tag);
    }
}
