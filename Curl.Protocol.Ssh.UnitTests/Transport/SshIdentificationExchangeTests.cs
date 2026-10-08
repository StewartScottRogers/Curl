using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshIdentificationExchangeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExchangeAsync_SendsLibssh2IdentificationThenCrLf()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes("SSH-2.0-OpenSSH_9.7\r\n");
        diagnostics.Arrange("scripted server text", SshAuthenticationDiagnostics.Text(Encoding.Latin1.GetString(scripted)));
        diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted);

        await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None);
        string written = Encoding.ASCII.GetString(connection.Written);
        diagnostics.Bytes("written", connection.Written);
        diagnostics.Act("written text", SshAuthenticationDiagnostics.Text(written));
        diagnostics.Act("flush count", connection.FlushCount);

        diagnostics.Diff("written text", "SSH-2.0-libssh2_1.11.1\r\n", written);
        diagnostics.Assert("flush count", 1, connection.FlushCount);
        Assert.AreEqual("SSH-2.0-libssh2_1.11.1\r\n", written);
        Assert.AreEqual(1, connection.FlushCount);
    }

    [TestMethod]
    public async Task ExchangeAsync_SkipsLinesBeforeTheIdentification()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes("hello\r\nworld\r\nSSH-2.0-OpenSSH_9.9\r\n");
        diagnostics.Arrange("scripted server text", SshAuthenticationDiagnostics.Text(Encoding.Latin1.GetString(scripted)));
        diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted);

        string server = await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None);
        diagnostics.Act("server identification", SshAuthenticationDiagnostics.Text(server));

        diagnostics.Diff("server identification", "SSH-2.0-OpenSSH_9.9", server);
        Assert.AreEqual("SSH-2.0-OpenSSH_9.9", server);
    }

    [TestMethod]
    public async Task ExchangeAsync_AcceptsAnyVersionAfterSsh_AsLibssh2Does()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes("SSH-1.5-OpenSSH_1\r\n");
        diagnostics.Arrange("scripted server text", SshAuthenticationDiagnostics.Text(Encoding.Latin1.GetString(scripted)));
        diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted);

        string server = await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None);
        diagnostics.Act("server identification", SshAuthenticationDiagnostics.Text(server));

        diagnostics.Diff("server identification", "SSH-1.5-OpenSSH_1", server);
        Assert.AreEqual("SSH-1.5-OpenSSH_1", server);
    }

    [TestMethod]
    public async Task ExchangeAsync_AcceptsAnIdentificationLongerThan255Bytes_AsLibssh2Does()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string longIdentification = "SSH-2.0-" + new string('x', 300);
        byte[] scripted = Encoding.ASCII.GetBytes(longIdentification + "\r\n");
        diagnostics.Arrange("identification length", longIdentification.Length);
        diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted);

        string server = await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None);
        diagnostics.Act("server identification length", server.Length);

        diagnostics.Diff("server identification", longIdentification, server);
        Assert.AreEqual(longIdentification, server);
    }

    [TestMethod]
    [DataRow("", DisplayName = "peer closes at once")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", DisplayName = "an HTTP response")]
    [DataRow("SSH-2.0-cut", DisplayName = "no line ending")]
    public async Task ExchangeAsync_NoSshLineBeforeTheClose_FailsAsCurlDoes(string sent)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes(sent);
        diagnostics.Arrange("sent", SshAuthenticationDiagnostics.Text(sent));
        diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None));
        diagnostics.ActFailure(failure);

        diagnostics.AssertFailure(CurlExitCode.FailedInit, "Failure establishing ssh session: -13, Failed getting banner", failure);
        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("Failure establishing ssh session: -13, Failed getting banner", failure.Message);
    }

    [TestMethod]
    [DataRow(false, "", DisplayName = "reset while curl waits for the banner")]
    [DataRow(false, "SSH-2.0-Open", DisplayName = "reset part-way through the banner")]
    [DataRow(true, "", DisplayName = "reset before curl sends its identification")]
    public async Task ExchangeAsync_PeerResetsTheConnection_FailsWithMinus43AsMeasured(bool resetOnWrite, string sent)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes(sent);
        diagnostics.Arrange("reset on write", resetOnWrite);
        diagnostics.Arrange("sent", SshAuthenticationDiagnostics.Text(sent));
        diagnostics.Bytes("scripted server bytes", scripted);
        ResettingConnection connection = new(resetOnWrite, scripted);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None));
        diagnostics.ActFailure(failure);

        diagnostics.AssertFailure(CurlExitCode.FailedInit, "Failure establishing ssh session: -43, Failed getting banner", failure);
        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("Failure establishing ssh session: -43, Failed getting banner", failure.Message);
    }

    [TestMethod]
    public async Task ExchangeAsync_LineLongerThanTheLimit_FailsAsNoBanner()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Encoding.ASCII.GetBytes(new string('x', SshIdentificationExchange.MaximumLineLength) + "\nSSH-2.0-x\r\n");
        diagnostics.Arrange("maximum line length", SshIdentificationExchange.MaximumLineLength);
        diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted);

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None));
        diagnostics.ActFailure(failure);

        diagnostics.Diff("error", "Failure establishing ssh session: -13, Failed getting banner", failure.Message);
        Assert.AreEqual("Failure establishing ssh session: -13, Failed getting banner", failure.Message);
    }
}
