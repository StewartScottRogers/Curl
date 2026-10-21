using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

[TestClass]
public sealed class HttpsServerConnectorTests
{
    private const string KeptOpenReply = "<reply>\n<data nonewline=\"yes\">\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi\n</data>\n</reply>\n";

    private const string ClosingReply = "<reply>\n<data nonewline=\"yes\">\nHTTP/1.1 200 OK\r\n\r\nbye swsclose\n</data>\n</reply>\n";

    private const string Request = "GET /1 HTTP/1.1\r\nHost: localhost\r\n\r\n";

    [TestMethod]
    public async Task ConnectAsync_AnotherPort_ReachesTheWrappedConnector()
    {
        using X509Certificate2 certificate = CreateCertificate();
        SwsHttpServerConnector sws = new(ParsedTestCase.From(KeptOpenReply));
        HttpsServerConnector connector = new(sws, certificate, new NoListenPortConnector(sws));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), CancellationToken.None);

        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_HttpsPort_AnswersEachRequestOverTlsAndRecordsItDecrypted()
    {
        using X509Certificate2 certificate = CreateCertificate();
        SwsHttpServerConnector sws = new(ParsedTestCase.From(KeptOpenReply));
        HttpsServerConnector connector = new(sws, certificate, sws);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", HttpsServerConnector.HttpsPort, false), CancellationToken.None);

        string first;
        string second;
        await using (SslStream client = await HandshakeAsync(result.Connection!))
        {
            first = await ExchangeAsync(client, 40);
            second = await ExchangeAsync(client, 40);
        }

        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi", first);
        Assert.AreEqual(first, second);
        Assert.AreEqual(Request + Request, Encoding.Latin1.GetString(sws.ReceivedBytes.Span));
        Assert.IsFalse(result.Connection!.IsSecure);
        Assert.AreEqual(HttpsServerConnector.HttpsPort, ((System.Net.IPEndPoint)result.Connection.RemoteEndPoint!).Port);
        Assert.IsNotNull(result.Connection.LocalEndPoint);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_ReplyClosingTheConnection_EndsTheTlsStreamCleanly()
    {
        using X509Certificate2 certificate = CreateCertificate();
        SwsHttpServerConnector sws = new(ParsedTestCase.From(ClosingReply));
        HttpsServerConnector connector = new(sws, certificate, sws);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", HttpsServerConnector.HttpsPort, false), CancellationToken.None);
        await using SslStream client = await HandshakeAsync(result.Connection!);

        await client.WriteAsync(Encoding.Latin1.GetBytes(Request));
        await client.FlushAsync();
        using MemoryStream reply = new();
        await client.CopyToAsync(reply);

        Assert.AreEqual("HTTP/1.1 200 OK\r\n\r\nbye swsclose", Encoding.Latin1.GetString(reply.ToArray()));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_ClientSpeakingPlainHttp_IsResetAsStunnelResetsIt()
    {
        // test1244: curl: (56) Recv failure: Connection was reset (GF-0081).
        using X509Certificate2 certificate = CreateCertificate();
        SwsHttpServerConnector sws = new(ParsedTestCase.From(KeptOpenReply));
        HttpsServerConnector connector = new(sws, certificate, sws);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", HttpsServerConnector.HttpsPort, false), CancellationToken.None);
        await using IConnection connection = result.Connection!;

        await connection.WriteAsync(Encoding.Latin1.GetBytes(Request), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[64], CancellationToken.None).AsTask());

        Assert.AreEqual(System.Net.Sockets.SocketError.ConnectionReset, ((System.Net.Sockets.SocketException)failure.InnerException!).SocketErrorCode);
        Assert.IsEmpty(sws.ReceivedBytes.ToArray());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_HttpsPortThroughTheProxyTunnel_RecordsTheConnectAsProxyBytesAndTheRequestDecrypted()
    {
        const string Connect = "CONNECT localhost:18989 HTTP/1.1\r\n\r\n";
        using X509Certificate2 certificate = CreateCertificate();
        SwsHttpServerConnector sws = new(ParsedTestCase.From(KeptOpenReply.Replace("<reply>\n", "<reply>\n<connect nonewline=\"yes\">\nHTTP/1.1 200 OK\r\n\r\n\n</connect>\n", StringComparison.Ordinal)));
        HttpsServerConnector https = new(sws, certificate, sws);
        sws.TunnelServerForPort = port => port == HttpsServerConnector.HttpsPort ? https : null;
        IConnection proxy = (await sws.ConnectAsync(new ConnectTarget("127.0.0.1", SwsHttpServerConnector.ProxyPort, false), CancellationToken.None)).Connection!;
        await proxy.WriteAsync(Encoding.Latin1.GetBytes(Connect), CancellationToken.None);
        byte[] connectReply = new byte[64];
        int connectReplyLength = await proxy.ReadAsync(connectReply, CancellationToken.None);

        string reply;
        await using (SslStream client = await HandshakeAsync(proxy))
        {
            reply = await ExchangeAsync(client, 40);
        }

        Assert.AreEqual("HTTP/1.1 200 OK\r\n\r\n", Encoding.Latin1.GetString(connectReply, 0, connectReplyLength));
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi", reply);
        Assert.AreEqual(Connect, Encoding.Latin1.GetString(sws.ProxyReceivedBytes.Span));
        Assert.AreEqual(Request, Encoding.Latin1.GetString(sws.ReceivedBytes.Span));
    }

    [TestMethod]
    public void LoadCertificate_PemWithKey_ReturnsTheCertificateWithItsKey()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 source = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        string path = Path.Combine(AppContext.BaseDirectory, "log", $"https-{Guid.NewGuid():N}.pem");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, source.ExportCertificatePem() + "\n" + key.ExportPkcs8PrivateKeyPem() + "\n");
        try
        {
            using X509Certificate2 loaded = HttpsServerConnector.LoadCertificate(path);

            Assert.IsTrue(loaded.HasPrivateKey);
            Assert.AreEqual(source.Thumbprint, loaded.Thumbprint);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<SslStream> HandshakeAsync(IConnection connection)
    {
        SslStream client = new(new ConnectionStream(connection), leaveInnerStreamOpen: false, static (_, _, _, _) => true);
        await client.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" });
        return client;
    }

    private static async Task<string> ExchangeAsync(SslStream client, int replyLength)
    {
        await client.WriteAsync(Encoding.Latin1.GetBytes(Request));
        await client.FlushAsync();
        byte[] reply = new byte[replyLength];
        int count = 0;
        int read;
        while (count < replyLength && (read = await client.ReadAsync(reply.AsMemory(count))) > 0)
        {
            count += read;
        }

        return Encoding.Latin1.GetString(reply, 0, count);
    }

    // A P-256 certificate, as UpstreamTestCertificateGenerator writes, reloaded through PKCS#12,
    // since Windows Schannel and macOS reject a server certificate whose key is ephemeral.
    private static X509Certificate2 CreateCertificate()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new("CN=localhost", key, HashAlgorithmName.SHA256);
        using X509Certificate2 ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null);
    }

    // The client's view of the connection as a stream, for SslStream.
    private sealed class ConnectionStream(IConnection connection) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            connection.ReadAsync(buffer, cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            connection.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            connection.WriteAsync(buffer, cancellationToken);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            connection.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
