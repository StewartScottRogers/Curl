using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Http2;
using Curl.Http3;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// Curl's QUIC client state driven by hand against the HTTP/3 stand-in, with the HTTP/3 framing
/// and QPACK a request and its reply need.
/// </summary>
internal static class Http3TestClient
{
    private static readonly X509Certificate2 Certificate = CreateRsaCertificate();

    internal static Http3ServerTlsSettings ServerSettings => Http3ServerTlsSettings.FromRsaCertificate(Certificate, []);

    internal static QuicClientConnectionState Create() =>
        new(
            new QuicClientSettings { Tls = QuicClientSettings.CreateOpenSslTlsSettings("localhost"), TransportParameters = QuicTransportParameters.CurlClientDefaults },
            SystemTlsRandomSource.Instance,
            AcceptingVerifier.Instance,
            TimeProvider.System);

    /// <summary>Sends the client's datagrams to the session and the answers back, until neither side has more.</summary>
    internal static async Task PumpAsync(QuicClientConnectionState client, Http3ServerSession session, IReadOnlyList<byte[]> toServer)
    {
        for (var round = 0; round < 50 && toServer.Count > 0; round++)
        {
            List<byte[]> toClient = [];
            foreach (byte[] datagram in toServer)
            {
                toClient.AddRange(await session.ReceiveAsync(datagram, CancellationToken.None));
            }

            toServer = [.. toClient.SelectMany(datagram => client.Receive(datagram)), .. client.TakeDatagramsToSend()];
        }
    }

    internal static byte[] Headers(params (string Name, string Value)[] fields) =>
        new Http3HeadersFrame(new QpackEncoder(0, 0).EncodeFieldSection(0, [.. fields.Select(f => new HeaderField(f.Name, f.Value))])).ToBytes();

    /// <summary>Reads what has arrived on the stream.</summary>
    internal static byte[] ReadAll(QuicStream stream)
    {
        var buffer = new byte[65536];
        return buffer.AsSpan(0, stream.Read(buffer)).ToArray();
    }

    /// <summary>Splits a reply stream into its decoded fields and its DATA bytes.</summary>
    internal static async Task<(IReadOnlyList<HeaderField> Fields, byte[] Body)> ReadReplyAsync(byte[] bytes)
    {
        var reader = new Http3FrameReader(new MemoryStream(bytes), 1 << 20);
        var decoder = new QpackDecoder(0, 0);
        IReadOnlyList<HeaderField> fields = [];
        var body = new MemoryStream();
        while (await reader.ReadFrameAsync(CancellationToken.None) is { } frame)
        {
            if (frame is Http3HeadersFrame headers)
            {
                decoder.TryDecodeFieldSection(0, headers.EncodedFieldSection.Span, out IReadOnlyList<HeaderField>? decoded);
                fields = decoded!;
            }
            else if (frame is Http3DataFrame data)
            {
                body.Write(data.Payload.Span);
            }
        }

        return (fields, body.ToArray());
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
