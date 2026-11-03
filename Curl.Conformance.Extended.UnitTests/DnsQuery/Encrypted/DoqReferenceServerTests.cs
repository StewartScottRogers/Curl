using System.Security.Cryptography.X509Certificates;
using Curl.Conformance.Extended.DnsQuery.Encrypted;
using Curl.Conformance.Extended.DnsQuery.Reference;
using Curl.Conformance.Extended.Harness;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Extended.UnitTests.DnsQuery.Encrypted;

/// <summary>The DoQ reference server driven by Curl.Quic's client state, for what curl's one-datagram query never shows.</summary>
[TestClass]
public sealed class DoqReferenceServerTests
{
    [TestMethod]
    public void Answer_QueryArrivesInTwoParts_IsAnsweredOnlyAfterItsFin()
    {
        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        using DoqReferenceServer server = DnsOverQuicCases.Server(certificate, (_, _) => [new DnsReferenceReply { Answers = [DnsReferenceRecord.A("example.com.", 300, "192.0.2.1")] }]);
        using QuicClientConnectionState client = Connect(server);
        byte[] message = DnsReferenceMessageWriter.WriteQuery(0, "example.com.", 1, DnsReferenceRecord.InternetClass);
        QuicStream stream = client.Streams.OpenBidirectional()!;

        stream.Write([0, (byte)message.Length], endStream: false);
        Pump(client, server, client.TakeDatagramsToSend());
        int receivedBeforeFin = server.Received.Count;
        stream.Write(message, endStream: true);
        Pump(client, server, client.TakeDatagramsToSend());

        Assert.AreEqual(0, receivedBeforeFin);
        CollectionAssert.AreEqual(new byte[] { 0, (byte)message.Length }.Concat(message).ToArray(), server.Received.Single().StreamBytes);
        byte[] answer = new byte[65536];
        int length = stream.Read(answer);
        Assert.AreEqual(length - 2, (answer[0] << 8) | answer[1]);
        Assert.AreEqual(0, (answer[2] << 8) | answer[3]);
    }

    private static QuicClientConnectionState Connect(DoqReferenceServer server)
    {
        var client = new QuicClientConnectionState(
            new QuicClientSettings { Tls = QuicClientSettings.CreateOpenSslTlsSettings("localhost") with { ApplicationProtocols = [DoqReferenceServer.ApplicationProtocol] } },
            SystemTlsRandomSource.Instance,
            AcceptingVerifier.Instance,
            TimeProvider.System);
        Pump(client, server, client.Start());
        Assert.IsTrue(client.IsConfirmed);
        return client;
    }

    private static void Pump(QuicClientConnectionState client, DoqReferenceServer server, IReadOnlyList<byte[]> toServer)
    {
        for (var round = 0; round < 50 && toServer.Count > 0; round++)
        {
            List<byte[]> toClient = [.. toServer.SelectMany(datagram => server.Answer(datagram)).Select(answer => answer.ToArray())];
            toServer = [.. toClient.SelectMany(datagram => client.Receive(datagram)), .. client.TakeDatagramsToSend()];
        }
    }

    private sealed class AcceptingVerifier : IServerCertificateVerifier
    {
        public static AcceptingVerifier Instance { get; } = new();

        public ServerCertificateVerdict Verify(ServerCertificateChain presented) => ServerCertificateVerdict.Accepted;
    }
}
