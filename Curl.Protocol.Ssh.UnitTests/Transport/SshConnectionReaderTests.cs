using System.Text;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshConnectionReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadLineAsync_StripsCrLfAndLeavesTheRestBuffered()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes("first\r\nsecond\nABCD");
        diagnostics.Bytes("scripted bytes", scripted);
        diagnostics.Arrange("maximum line length", 100);
        SshConnectionReader reader = new(new ScriptedConnection(scripted));

        string? first = await reader.ReadLineAsync(100, CancellationToken.None);
        string? second = await reader.ReadLineAsync(100, CancellationToken.None);
        byte[] rest = await reader.ReadExactlyAsync(4, CancellationToken.None);
        diagnostics.Act("first line", SshAuthenticationDiagnostics.Text(first ?? "(null)"));
        diagnostics.Act("second line", SshAuthenticationDiagnostics.Text(second ?? "(null)"));
        diagnostics.Bytes("rest", rest);

        diagnostics.Assert("first line", "first", first);
        diagnostics.Assert("second line", "second", second);
        diagnostics.Diff("rest", Encoding.ASCII.GetBytes("ABCD"), rest);
        Assert.AreEqual("first", first);
        Assert.AreEqual("second", second);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("ABCD"), rest);
    }

    [TestMethod]
    public async Task ReadLineAsync_LineSplitAcrossReads_IsJoined()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes("SSH-2.0-OpenSSH_9.7\r\n");
        diagnostics.Bytes("scripted bytes", scripted);
        diagnostics.Arrange("chunk size", 1);
        SshConnectionReader reader = new(ScriptedConnection.InChunks(scripted, 1));

        string? line = await reader.ReadLineAsync(100, CancellationToken.None);
        diagnostics.Act("line", line ?? "(null)");

        diagnostics.Assert("line", "SSH-2.0-OpenSSH_9.7", line);
        Assert.AreEqual("SSH-2.0-OpenSSH_9.7", line);
    }

    [TestMethod]
    public async Task ReadLineAsync_KeepsEveryByteAsLatin1()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = [0xE9, 0xFF, (byte)'\n'];
        diagnostics.Arrange("scripted text", SshAuthenticationDiagnostics.Text(Encoding.Latin1.GetString(scripted)));
        diagnostics.Bytes("scripted bytes", scripted);
        SshConnectionReader reader = new(new ScriptedConnection(scripted));

        string? line = await reader.ReadLineAsync(100, CancellationToken.None);
        diagnostics.Act("line", SshAuthenticationDiagnostics.Text(line ?? "(null)"));

        diagnostics.Assert("line", "éÿ", line);
        Assert.AreEqual("éÿ", line);
    }

    [TestMethod]
    public async Task ReadLineAsync_PeerClosesBeforeLf_ReturnsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes("partial");
        diagnostics.Arrange("scripted text", SshAuthenticationDiagnostics.Text(Encoding.Latin1.GetString(scripted)));
        diagnostics.Bytes("scripted bytes", scripted);
        SshConnectionReader reader = new(new ScriptedConnection(scripted));

        string? line = await reader.ReadLineAsync(100, CancellationToken.None);
        diagnostics.Act("line", line ?? "(null)");

        diagnostics.Assert("line", "(null)", line ?? "(null)");
        Assert.IsNull(line);
    }

    [TestMethod]
    public async Task ReadLineAsync_LineLongerThanTheMaximum_ReturnsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes("12345\n");
        diagnostics.Bytes("scripted bytes", scripted);
        diagnostics.Arrange("maximum line length", 5);
        SshConnectionReader reader = new(new ScriptedConnection(scripted));

        string? line = await reader.ReadLineAsync(5, CancellationToken.None);
        diagnostics.Act("line", line ?? "(null)");

        diagnostics.Assert("line", "(null)", line ?? "(null)");
        Assert.IsNull(line);
    }

    [TestMethod]
    public async Task ReadLineAsync_LineExactlyTheMaximum_IsRead()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes("1234\n");
        diagnostics.Bytes("scripted bytes", scripted);
        diagnostics.Arrange("maximum line length", 5);
        SshConnectionReader reader = new(new ScriptedConnection(scripted));

        string? line = await reader.ReadLineAsync(5, CancellationToken.None);
        diagnostics.Act("line", line ?? "(null)");

        diagnostics.Assert("line", "1234", line);
        Assert.AreEqual("1234", line);
    }

    [TestMethod]
    public async Task ReadExactlyAsync_JoinsChunks()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("chunk 0", [1, 2]);
        diagnostics.Bytes("chunk 1", [3]);
        diagnostics.Bytes("chunk 2", [4, 5, 6]);
        SshConnectionReader reader = new(new ScriptedConnection([1, 2], [3], [4, 5, 6]));
        diagnostics.Arrange("reads", "4 bytes then 2 bytes");

        byte[] firstRead = await reader.ReadExactlyAsync(4, CancellationToken.None);
        byte[] secondRead = await reader.ReadExactlyAsync(2, CancellationToken.None);
        diagnostics.Bytes("first read", firstRead);
        diagnostics.Bytes("second read", secondRead);
        diagnostics.Act("first read length", firstRead.Length);
        diagnostics.Act("second read length", secondRead.Length);

        diagnostics.Diff("first read", new byte[] { 1, 2, 3, 4 }, firstRead);
        diagnostics.Diff("second read", new byte[] { 5, 6 }, secondRead);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, firstRead);
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, secondRead);
    }

    [TestMethod]
    public async Task ReadExactlyAsync_PeerClosesFirst_ThrowsEndOfStream()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("scripted bytes", [1, 2]);
        diagnostics.Arrange("bytes requested", 3);
        SshConnectionReader reader = new(new ScriptedConnection([1, 2]));

        EndOfStreamException failure = await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await reader.ReadExactlyAsync(3, CancellationToken.None));
        diagnostics.Act("exception", $"{failure.GetType().Name}: {SshAuthenticationDiagnostics.Text(failure.Message)}");

        diagnostics.Assert("exception type", nameof(EndOfStreamException), failure.GetType().Name);
        Assert.AreEqual(typeof(EndOfStreamException), failure.GetType());
    }

    [TestMethod]
    public async Task ReadExactlyAsync_ConnectionReset_ThrowsSshConnectionLostWrappingTheReset()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Bytes("scripted bytes before the reset", [1, 2]);
        diagnostics.Arrange("bytes requested", 3);
        SshConnectionReader reader = new(new ResettingConnection(false, [1, 2]));

        SshConnectionLostException lost = await Assert.ThrowsExactlyAsync<SshConnectionLostException>(async () => await reader.ReadExactlyAsync(3, CancellationToken.None));
        Exception? inner = lost.InnerException?.InnerException;
        diagnostics.Act("exception", $"{lost.GetType().Name}: {SshAuthenticationDiagnostics.Text(lost.Message)}");
        diagnostics.Act("inner of inner", inner?.GetType().Name ?? "(null)");

        diagnostics.Assert("inner of inner type", nameof(System.Net.Sockets.SocketException), inner?.GetType().Name ?? "(null)");
        Assert.IsInstanceOfType<System.Net.Sockets.SocketException>(lost.InnerException?.InnerException);
    }
}
