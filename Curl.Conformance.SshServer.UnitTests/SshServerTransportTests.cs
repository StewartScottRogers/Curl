using System.Text;
using Curl.Protocol.Ssh;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerTransportTests
{
    private static readonly byte[] Ignore = [SshMessageNumber.Ignore, 0, 0, 0, 0];

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

    [TestMethod]
    public async Task ReadPacketAsync_ClientSendsAPayload_ServerReadsTheSamePayload()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, new SystemSshRandomSource());

        await new SshPacketWriter(client, new SystemSshRandomSource()).WriteAsync(Ignore, CancellationToken.None);
        byte[] received = await transport.ReadPacketAsync(CancellationToken.None);

        CollectionAssert.AreEqual(Ignore, received);
    }

    [TestMethod]
    public async Task WritePacketAsync_ServerSendsAPayload_ClientReadsTheSamePayload()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, new SystemSshRandomSource());

        await transport.WritePacketAsync(Ignore, CancellationToken.None);
        byte[] received = await new SshPacketReader(new SshConnectionReader(client)).ReadAsync(CancellationToken.None);

        CollectionAssert.AreEqual(Ignore, received);
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_ClientSendsAnotherMessageFirst_ThrowsInvalidDataException()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, new SystemSshRandomSource());

        await new SshPacketWriter(client, new SystemSshRandomSource()).WriteAsync(Ignore, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await transport.ExchangeKeysAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ExchangeKeysAsync_ClientOffersNoSharedKeyExchange_ThrowsInvalidDataException()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, new SystemSshRandomSource());
        SshAlgorithmPreferences preferences = new(["diffie-hellman-group14-sha256"], ["ssh-ed25519"], ["aes128-ctr"], ["hmac-sha2-256"], ["none"]);
        byte[] kexInit = SshKexInit.ForClient(preferences, SshAlgorithmCatalogue.Implemented, new SystemSshRandomSource()).ToPayload();

        await new SshPacketWriter(client, new SystemSshRandomSource()).WriteAsync(kexInit, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await transport.ExchangeKeysAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task AcceptServiceRequestAsync_ClientAsksForAnotherService_ThrowsInvalidDataException()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        SshServerTransport transport = new(server, new SystemSshRandomSource());
        SshWireWriter request = new();
        request.WriteByte(SshMessageNumber.ServiceRequest);
        request.WriteString("ssh-connection"u8);

        await new SshPacketWriter(client, new SystemSshRandomSource()).WriteAsync(request.ToArray(), CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await transport.AcceptServiceRequestAsync(CancellationToken.None));
    }
}
