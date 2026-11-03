using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

[TestClass]
public sealed class Http3ServerConnectionDatagramTests
{
    [TestMethod]
    public void AcceptDatagram_ClientSendsDatagrams_ReturnsThemInArrivalOrderThenNull()
    {
        using var server = new Http3ServerConnection(Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance);
        using var client = Connect(server, advertise: true);
        client.SendDatagram(new byte[] { 1, 2, 3 });
        client.SendDatagram(new byte[] { 4, 5 });

        Pump(client, server, client.TakeDatagramsToSend(), []);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, server.AcceptDatagram());
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, server.AcceptDatagram());
        Assert.IsNull(server.AcceptDatagram());
    }

    [TestMethod]
    public void SendDatagram_ClientAdvertisedTheParameter_ClientReceivesThePayload()
    {
        using var server = new Http3ServerConnection(Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance);
        using var client = Connect(server, advertise: true);

        server.SendDatagram(new byte[] { 9, 8, 7 });
        Pump(client, server, [], server.TakeDatagramsToSend());

        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, client.TakeReceivedDatagram()!.Value.ToArray());
        Assert.IsNull(client.TakeReceivedDatagram());
    }

    [TestMethod]
    public void SendDatagram_ClientDidNotAdvertiseTheParameter_Throws()
    {
        using var server = new Http3ServerConnection(Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance);
        using var client = Connect(server, advertise: false);

        Assert.ThrowsExactly<InvalidOperationException>(() => server.SendDatagram(new byte[] { 1 }));
    }

    [TestMethod]
    public void SendDatagram_PayloadExceedsTheAdvertisedSize_Throws()
    {
        using var server = new Http3ServerConnection(Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance);
        using var client = Connect(server, advertise: true);

        Assert.ThrowsExactly<InvalidOperationException>(() => server.SendDatagram(new byte[1198]));
        server.SendDatagram(new byte[1197]);
    }

    [TestMethod]
    public void SendDatagram_HandshakeNotComplete_Throws()
    {
        using var server = new Http3ServerConnection(Http3TestClient.ServerSettings, SystemTlsRandomSource.Instance);

        Assert.ThrowsExactly<InvalidOperationException>(() => server.SendDatagram(new byte[] { 1 }));
    }

    private static QuicClientConnectionState Connect(Http3ServerConnection server, bool advertise)
    {
        var parameters = advertise ? QuicTransportParameters.CurlClientDefaults with { MaxDatagramFrameSize = 1200 } : QuicTransportParameters.CurlClientDefaults;
        var client = new QuicClientConnectionState(
            new QuicClientSettings { Tls = QuicClientSettings.CreateOpenSslTlsSettings("localhost"), TransportParameters = parameters },
            SystemTlsRandomSource.Instance,
            AcceptingVerifier.Instance,
            TimeProvider.System);
        Pump(client, server, client.Start(), []);
        Assert.IsTrue(client.IsConfirmed);
        return client;
    }

    private static void Pump(QuicClientConnectionState client, Http3ServerConnection server, IReadOnlyList<byte[]> toServer, IReadOnlyList<byte[]> toClient)
    {
        for (var round = 0; round < 50 && (toServer.Count > 0 || toClient.Count > 0); round++)
        {
            var nextToClient = toServer.SelectMany(datagram => server.Receive(datagram)).ToList();
            toServer = toClient.Concat(nextToClient).SelectMany(datagram => client.Receive(datagram)).ToList();
            toClient = [];
        }
    }

    private sealed class AcceptingVerifier : IServerCertificateVerifier
    {
        public static AcceptingVerifier Instance { get; } = new();

        public ServerCertificateVerdict Verify(ServerCertificateChain presented) => ServerCertificateVerdict.Accepted;
    }
}
