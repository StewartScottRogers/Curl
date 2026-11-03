using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Conformance.Extended.WebTransport.Server;
using Curl.Conformance.Http3Server;
using Curl.Http2;
using Curl.Http3;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Extended.UnitTests.WebTransport.Server;

/// <summary>Curl.Quic's client state driven by hand against the WebTransport reference server.</summary>
internal static class WebTransportTestClient
{
    private static readonly X509Certificate2 Certificate = CreateRsaCertificate();

    internal static WebTransportReferenceServer CreateServer(WebTransportServerScript script) =>
        new(Http3ServerTlsSettings.FromRsaCertificate(Certificate, []), script, SystemTlsRandomSource.Instance);

    /// <summary>Returns a client whose handshake with the server is confirmed.</summary>
    internal static QuicClientConnectionState Connect(WebTransportReferenceServer server)
    {
        var client = new QuicClientConnectionState(
            new QuicClientSettings
            {
                Tls = QuicClientSettings.CreateOpenSslTlsSettings("localhost"),
                TransportParameters = QuicTransportParameters.CurlClientDefaults with { MaxDatagramFrameSize = 1200 },
            },
            SystemTlsRandomSource.Instance,
            AcceptingVerifier.Instance,
            TimeProvider.System);
        Pump(client, server, client.Start());
        Assert.IsTrue(client.IsConfirmed);
        return client;
    }

    /// <summary>Sends what the client has queued and pumps the answers until neither side has more.</summary>
    internal static void Flush(QuicClientConnectionState client, WebTransportReferenceServer server) =>
        Pump(client, server, client.TakeDatagramsToSend());

    internal static byte[] Read(QuicStream stream)
    {
        var buffer = new byte[65536];
        return buffer.AsSpan(0, stream.Read(buffer)).ToArray();
    }

    internal static byte[] ConnectHeaders() =>
        new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(0, [
            new HeaderField(":method", "CONNECT"),
            new HeaderField(":protocol", "webtransport"),
            new HeaderField(":scheme", "https"),
            new HeaderField(":authority", "localhost:4433"),
            new HeaderField(":path", "/echo"),
        ])).ToBytes();

    /// <summary>Opens the CONNECT stream and returns it once the server has answered.</summary>
    internal static QuicStream OpenSession(QuicClientConnectionState client, WebTransportReferenceServer server)
    {
        QuicStream stream = client.Streams.OpenBidirectional()!;
        stream.Write(ConnectHeaders(), endStream: false);
        Flush(client, server);
        return stream;
    }

    /// <summary>Splits the bytes of a server stream into its HTTP/3 frames' types and payloads.</summary>
    internal static List<(long Type, byte[] Payload)> Frames(byte[] bytes)
    {
        var reader = new WebTransportServerBuffer();
        reader.Append(bytes);
        List<(long, byte[])> frames = [];
        while (reader.TryReadTypeLengthValue(out long type, out byte[] payload))
        {
            frames.Add((type, payload));
        }

        return frames;
    }

    private static void Pump(QuicClientConnectionState client, WebTransportReferenceServer server, IReadOnlyList<byte[]> toServer)
    {
        for (var round = 0; round < 50 && toServer.Count > 0; round++)
        {
            List<byte[]> toClient = [.. toServer.SelectMany(datagram => server.Answer(datagram)).Select(answer => answer.ToArray())];
            toServer = [.. toClient.SelectMany(datagram => client.Receive(datagram)), .. client.TakeDatagramsToSend()];
        }
    }

    private static X509Certificate2 CreateRsaCertificate()
    {
        using RSA key = RSA.Create(2048);
        return new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private sealed class AcceptingVerifier : IServerCertificateVerifier
    {
        public static AcceptingVerifier Instance { get; } = new();

        public ServerCertificateVerdict Verify(ServerCertificateChain presented) => ServerCertificateVerdict.Accepted;
    }
}
