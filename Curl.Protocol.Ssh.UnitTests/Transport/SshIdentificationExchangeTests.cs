using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshIdentificationExchangeTests
{
    [TestMethod]
    public async Task ExchangeAsync_SendsLibssh2IdentificationThenCrLf()
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("SSH-2.0-OpenSSH_9.7\r\n"));

        await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None);

        Assert.AreEqual("SSH-2.0-libssh2_1.11.1\r\n", Encoding.ASCII.GetString(connection.Written));
        Assert.AreEqual(1, connection.FlushCount);
    }

    [TestMethod]
    public async Task ExchangeAsync_SkipsLinesBeforeTheIdentification()
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("hello\r\nworld\r\nSSH-2.0-OpenSSH_9.9\r\n"));

        string server = await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None);

        Assert.AreEqual("SSH-2.0-OpenSSH_9.9", server);
    }

    [TestMethod]
    public async Task ExchangeAsync_AcceptsAnyVersionAfterSsh_AsLibssh2Does()
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes("SSH-1.5-OpenSSH_1\r\n"));

        string server = await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None);

        Assert.AreEqual("SSH-1.5-OpenSSH_1", server);
    }

    [TestMethod]
    public async Task ExchangeAsync_AcceptsAnIdentificationLongerThan255Bytes_AsLibssh2Does()
    {
        string longIdentification = "SSH-2.0-" + new string('x', 300);
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes(longIdentification + "\r\n"));

        string server = await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None);

        Assert.AreEqual(longIdentification, server);
    }

    [TestMethod]
    [DataRow("", DisplayName = "peer closes at once")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", DisplayName = "an HTTP response")]
    [DataRow("SSH-2.0-cut", DisplayName = "no line ending")]
    public async Task ExchangeAsync_NoSshLineBeforeTheClose_FailsAsCurlDoes(string sent)
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes(sent));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("Failure establishing ssh session: -13, Failed getting banner", failure.Message);
    }

    [TestMethod]
    public async Task ExchangeAsync_LineLongerThanTheLimit_FailsAsNoBanner()
    {
        ScriptedConnection connection = new(Encoding.ASCII.GetBytes(new string('x', SshIdentificationExchange.MaximumLineLength) + "\nSSH-2.0-x\r\n"));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await SshIdentificationExchange.ExchangeAsync(connection, new SshConnectionReader(connection), CancellationToken.None));

        Assert.AreEqual("Failure establishing ssh session: -13, Failed getting banner", failure.Message);
    }
}
