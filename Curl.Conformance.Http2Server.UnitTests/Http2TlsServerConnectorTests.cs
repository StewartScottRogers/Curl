using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

[TestClass]
public sealed class Http2TlsServerConnectorTests
{
    private const string Reply = "HTTP/1.1 200 OK\r\nContent-Length: 6\r\nConnection: close\r\n\r\n-foo-\n";

    private static readonly HeaderField[] Get2403 =
        [new(":method", "GET"), new(":scheme", "https"), new(":authority", "127.0.0.1:9018"), new(":path", "/2403"), new("user-agent", "curl/8.22.0"), new("accept", "*/*")];

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_TlsClientOfferingH2_NegotiatesH2AndRelaysTheGetToTheBackendAsHttps()
    {
        using X509Certificate2 certificate = CreateCertificate();
        var backend = new RecordingBackendConnector(Reply);
        var connector = new Http2TlsServerConnector(backend, certificate);

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 9018, false), CancellationToken.None);
        var tls = new SslStream(new ConnectionStream(result.Connection!), leaveInnerStreamOpen: false);
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            ApplicationProtocols = [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11],
            RemoteCertificateValidationCallback = static (_, _, _, _) => true,
        });
        await tls.WriteAsync("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray());
        await Http2FrameCodec.WriteAsync(tls, Http2FrameFactory.CreateSettings([]), CancellationToken.None);
        await Http2FrameCodec.WriteAsync(tls, Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode(Get2403), isEndStream: true, isEndHeaders: true), CancellationToken.None);
        Http2Frame frame;
        do
        {
            frame = await Http2FrameCodec.ReadAsync(tls, Http2FrameCodec.LargestMaximumFrameSize, CancellationToken.None) ?? throw new EndOfStreamException();
        }
        while (frame.Type != Http2FrameType.Headers);
        await Http2FrameCodec.WriteAsync(tls, Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty), CancellationToken.None);
        SslApplicationProtocol negotiated = tls.NegotiatedApplicationProtocol;
        await tls.DisposeAsync();
        await connector.Sessions.Single();

        Assert.AreEqual(SslApplicationProtocol.Http2, negotiated);
        Assert.StartsWith("GET /2403 HTTP/1.1\r\nHost: 127.0.0.1:9018\r\n", backend.Requests.Single());
        Assert.Contains("X-Forwarded-Proto: https\r\n", backend.Requests.Single());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_ClientSendsPlainTextInsteadOfAHandshake_ClosesTheConnection()
    {
        using X509Certificate2 certificate = CreateCertificate();
        var backend = new RecordingBackendConnector(Reply);
        var connector = new Http2TlsServerConnector(backend, certificate);

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 9018, false), CancellationToken.None);
        IConnection connection = result.Connection!;
        await connection.WriteAsync("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray(), CancellationToken.None);
        await connector.Sessions.Single();
        int read = await connection.ReadAsync(new byte[16], CancellationToken.None);
        await connection.DisposeAsync();

        Assert.AreEqual(0, read);
        Assert.IsEmpty(backend.Requests);
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null);
    }
}
