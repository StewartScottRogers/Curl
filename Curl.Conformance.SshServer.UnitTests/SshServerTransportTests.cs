using System.Text;
using Curl.Protocol.Ssh;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerTransportTests
{
    [TestMethod]
    public async Task ExchangeIdentificationAsync_LinesBeforeTheIdentification_SkipsThem()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, new SystemSshRandomSource());

        await client.WriteAsync(Encoding.ASCII.GetBytes("hello\r\nSSH-2.0-test\r\n"), CancellationToken.None);
        string identification = await transport.ExchangeIdentificationAsync(CancellationToken.None);

        Assert.AreEqual("SSH-2.0-test", identification);
    }

    [TestMethod]
    public async Task ExchangeIdentificationAsync_ClientClosesFirst_ThrowsIOException()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, new SystemSshRandomSource());

        await client.DisposeAsync();

        await Assert.ThrowsExactlyAsync<IOException>(async () => await transport.ExchangeIdentificationAsync(CancellationToken.None));
    }
}
