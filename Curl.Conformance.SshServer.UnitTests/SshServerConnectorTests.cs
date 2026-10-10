using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_ClientOffersAes128CtrWithHmacSha256_KeysAreExchangedAndTheServiceAcceptedEncrypted()
    {
        SshServerConnector connector = new(new SystemSshRandomSource());
        SshTransport client = await ConnectAsync(connector, "aes128-ctr", "hmac-sha2-256");

        SshKeyExchangeResult result = await HandshakeAsync(client);
        SshServerTransport session = await connector.Sessions.Single();

        Assert.AreEqual("curve25519-sha256", result.Algorithms.KeyExchange);
        Assert.AreEqual("aes128-ctr", result.Algorithms.CipherServerToClient);
        Assert.AreEqual("hmac-sha2-256", result.Algorithms.MacClientToServer);
        CollectionAssert.AreEqual(SshServerHostKey.Blob, result.HostKey);
        CollectionAssert.AreEqual(client.SessionIdentifier, session.SessionIdentifier);
        Assert.AreEqual(SshServerTransport.ServerIdentification, client.ServerIdentification);
        Assert.AreEqual(SshIdentificationExchange.ClientIdentification, session.ClientIdentification);
    }

    [TestMethod]
    public async Task ConnectAsync_ClientOffersChaCha20Poly1305_KeysAreExchangedAndTheServiceAcceptedEncrypted()
    {
        SshServerConnector connector = new(new SystemSshRandomSource());
        SshTransport client = await ConnectAsync(connector, "chacha20-poly1305@openssh.com", "hmac-sha2-256");

        SshKeyExchangeResult result = await HandshakeAsync(client);
        SshServerTransport session = await connector.Sessions.Single();

        Assert.AreEqual("chacha20-poly1305@openssh.com", result.Algorithms.CipherClientToServer);
        Assert.IsNull(result.Algorithms.MacServerToClient);
        CollectionAssert.AreEqual(client.SessionIdentifier, session.SessionIdentifier);
    }

    [TestMethod]
    public async Task ConnectAsync_ClientIsWinCngsLibssh2_Group14AndRsaHostKeyGiveMatchingSessionIdentifiers()
    {
        SshServerConnector connector = new(new SystemSshRandomSource());
        SshTransport client = await ConnectAsync(connector, SshAlgorithmPreferences.WindowsReference);

        SshKeyExchangeResult result = await HandshakeAsync(client);
        SshServerTransport session = await connector.Sessions.Single();

        Assert.AreEqual("diffie-hellman-group14-sha256", result.Algorithms.KeyExchange);
        Assert.AreEqual("rsa-sha2-512", result.Algorithms.ServerHostKey);
        CollectionAssert.AreEqual(SshServerRsaHostKey.Blob, result.HostKey);
        CollectionAssert.AreEqual(client.SessionIdentifier, session.SessionIdentifier);
    }

    [TestMethod]
    [DataRow("rsa-sha2-256")]
    [DataRow("ssh-rsa")]
    public async Task ConnectAsync_ClientOffersOneRsaSignature_TheServerSignsWithIt(string hostKeyAlgorithm)
    {
        SshServerConnector connector = new(new SystemSshRandomSource());
        SshAlgorithmPreferences preferences = new(["curve25519-sha256"], [hostKeyAlgorithm], ["aes128-ctr"], ["hmac-sha2-256"], ["none"]);
        SshTransport client = await ConnectAsync(connector, preferences);

        SshKeyExchangeResult result = await HandshakeAsync(client);
        SshServerTransport session = await connector.Sessions.Single();

        Assert.AreEqual(hostKeyAlgorithm, result.Algorithms.ServerHostKey);
        CollectionAssert.AreEqual(client.SessionIdentifier, session.SessionIdentifier);
    }

    [TestMethod]
    public async Task ConnectAsync_TwoConnections_ListsBothSessionsInOrder()
    {
        SshServerConnector connector = new(new SystemSshRandomSource());

        await ConnectAsync(connector, "aes128-ctr", "hmac-sha2-256");
        await ConnectAsync(connector, "aes128-ctr", "hmac-sha2-256");

        Assert.HasCount(2, connector.Sessions);
    }

    private static async Task<SshKeyExchangeResult> HandshakeAsync(SshTransport client)
    {
        SshNegotiatedHandshake handshake = await client.NegotiateAlgorithmsAsync(CancellationToken.None);
        SshKeyExchangeResult result = await client.ExchangeKeysAsync(handshake, CancellationToken.None);
        await new SshUserAuthentication(client, Encoding.UTF8).RequestServiceAsync(CancellationToken.None);
        return result;
    }

    private static Task<SshTransport> ConnectAsync(SshServerConnector connector, string cipher, string mac) =>
        ConnectAsync(connector, new SshAlgorithmPreferences(["curve25519-sha256"], ["ssh-ed25519"], [cipher], [mac], ["none"]));

    private static async Task<SshTransport> ConnectAsync(SshServerConnector connector, SshAlgorithmPreferences preferences)
    {
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 22, UseTls: false), CancellationToken.None);
        return new SshTransport(result.Connection!, preferences, SshAlgorithmCatalogue.Implemented, new SystemSshRandomSource(), new SystemSshEphemeralKeySource());
    }
}
