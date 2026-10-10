using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_ClientIdentificationExchange_EachSideSeesTheOthersIdentification()
    {
        SshServerConnector connector = new(new SystemSshRandomSource());
        IConnection client = await ConnectAsync(connector);

        string serverIdentification = await SshIdentificationExchange.ExchangeAsync(client, new SshConnectionReader(client), CancellationToken.None);
        SshServerTransport transport = await connector.Sessions.Single();

        Assert.AreEqual(SshServerTransport.ServerIdentification, serverIdentification);
        Assert.AreEqual(SshIdentificationExchange.ClientIdentification, transport.ClientIdentification);
    }

    [TestMethod]
    public async Task ReadPacketAsync_ClientSendsItsKexInit_ServerReadsTheSamePayload()
    {
        SshServerConnector connector = new(new SystemSshRandomSource());
        IConnection client = await ConnectAsync(connector);
        SshConnectionReader clientReader = new(client);
        await SshIdentificationExchange.ExchangeAsync(client, clientReader, CancellationToken.None);
        SshServerTransport transport = await connector.Sessions.Single();
        byte[] kexInit = SshKexInit.ForClient(SshAlgorithmPreferences.OpenSslReference, SshAlgorithmCatalogue.Implemented, new SystemSshRandomSource()).ToPayload();

        await new SshPacketWriter(client, new SystemSshRandomSource()).WriteAsync(kexInit, CancellationToken.None);
        byte[] received = await transport.ReadPacketAsync(CancellationToken.None);

        CollectionAssert.AreEqual(kexInit, received);
    }

    [TestMethod]
    public async Task WritePacketAsync_ServerSendsAPayload_ClientReadsTheSamePayload()
    {
        SshServerConnector connector = new(new SystemSshRandomSource());
        IConnection client = await ConnectAsync(connector);
        SshConnectionReader clientReader = new(client);
        await SshIdentificationExchange.ExchangeAsync(client, clientReader, CancellationToken.None);
        SshServerTransport transport = await connector.Sessions.Single();
        byte[] ignore = [(byte)SshMessageNumber.Ignore, 0, 0, 0, 0];

        await transport.WritePacketAsync(ignore, CancellationToken.None);
        byte[] received = await new SshPacketReader(clientReader).ReadAsync(CancellationToken.None);

        CollectionAssert.AreEqual(ignore, received);
    }

    [TestMethod]
    public async Task ConnectAsync_TwoConnections_ListsBothSessionsInOrder()
    {
        SshServerConnector connector = new(new SystemSshRandomSource());

        await ConnectAsync(connector);
        await ConnectAsync(connector);

        Assert.HasCount(2, connector.Sessions);
    }

    private static async Task<IConnection> ConnectAsync(SshServerConnector connector)
    {
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 22, UseTls: false), CancellationToken.None);
        return result.Connection!;
    }
}
