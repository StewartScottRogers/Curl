using System.Text;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshConnectionReaderTests
{
    [TestMethod]
    public async Task ReadLineAsync_StripsCrLfAndLeavesTheRestBuffered()
    {
        SshConnectionReader reader = new(new ScriptedConnection(Encoding.ASCII.GetBytes("first\r\nsecond\nABCD")));

        Assert.AreEqual("first", await reader.ReadLineAsync(100, CancellationToken.None));
        Assert.AreEqual("second", await reader.ReadLineAsync(100, CancellationToken.None));
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("ABCD"), await reader.ReadExactlyAsync(4, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineSplitAcrossReads_IsJoined()
    {
        SshConnectionReader reader = new(ScriptedConnection.InChunks(Encoding.ASCII.GetBytes("SSH-2.0-OpenSSH_9.7\r\n"), 1));

        Assert.AreEqual("SSH-2.0-OpenSSH_9.7", await reader.ReadLineAsync(100, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadLineAsync_KeepsEveryByteAsLatin1()
    {
        SshConnectionReader reader = new(new ScriptedConnection([0xE9, 0xFF, (byte)'\n']));

        Assert.AreEqual("éÿ", await reader.ReadLineAsync(100, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadLineAsync_PeerClosesBeforeLf_ReturnsNull()
    {
        SshConnectionReader reader = new(new ScriptedConnection(Encoding.ASCII.GetBytes("partial")));

        Assert.IsNull(await reader.ReadLineAsync(100, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineLongerThanTheMaximum_ReturnsNull()
    {
        SshConnectionReader reader = new(new ScriptedConnection(Encoding.ASCII.GetBytes("12345\n")));

        Assert.IsNull(await reader.ReadLineAsync(5, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineExactlyTheMaximum_IsRead()
    {
        SshConnectionReader reader = new(new ScriptedConnection(Encoding.ASCII.GetBytes("1234\n")));

        Assert.AreEqual("1234", await reader.ReadLineAsync(5, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadExactlyAsync_JoinsChunks()
    {
        SshConnectionReader reader = new(new ScriptedConnection([1, 2], [3], [4, 5, 6]));

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await reader.ReadExactlyAsync(4, CancellationToken.None));
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, await reader.ReadExactlyAsync(2, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadExactlyAsync_PeerClosesFirst_ThrowsEndOfStream()
    {
        SshConnectionReader reader = new(new ScriptedConnection([1, 2]));

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await reader.ReadExactlyAsync(3, CancellationToken.None));
    }
}
