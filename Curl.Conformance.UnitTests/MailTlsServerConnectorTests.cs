using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="MailTlsServerConnector"/> serves the plain mail servers behind implicit TLS on their TLS ports and passes every other port on.</summary>
[TestClass]
public sealed class MailTlsServerConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_AnotherPort_ReachesTheWrappedConnectorAsItIs()
    {
        using X509Certificate2 certificate = CreateCertificate();
        RecordingConnector backend = new(new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));
        MailTlsServerConnector connector = new(certificate, backend);

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), TestContext.CancellationToken);

        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual(NoListenPortConnector.NoListenPort, backend.Ports.Single());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    [DataRow(MailTlsServerConnector.SmtpsPort, SmtpServerConnector.SmtpPort)]
    [DataRow(MailTlsServerConnector.ImapsPort, ImapServerConnector.ImapPort)]
    [DataRow(MailTlsServerConnector.Pop3sPort, Pop3ServerConnector.Pop3Port)]
    public async Task ConnectAsync_TlsPort_ReachesThePlainServerBehindTls(int tlsPort, int plainPort)
    {
        using X509Certificate2 certificate = CreateCertificate();
        RecordingConnector backend = new(new LineProtocolServerConnector(() => new EchoResponder()));
        MailTlsServerConnector connector = new(certificate, backend);

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", tlsPort, false), TestContext.CancellationToken);
        await using (SslStream client = await HandshakeAsync(result.Connection!))
        {
            Assert.AreEqual("hello\r\n", await ReadAsync(client, 7));
        }

        Assert.AreEqual(plainPort, backend.Ports.Single());
        Assert.AreEqual(tlsPort, ((IPEndPoint)result.Connection!.RemoteEndPoint!).Port);
        Assert.IsNotNull(result.Connection.LocalEndPoint);
        Assert.IsFalse(result.Connection.IsSecure);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_SmtpsPort_RelaysTheCommandsDecrypted()
    {
        using X509Certificate2 certificate = CreateCertificate();
        SmtpServerConnector smtp = new(
            ParsedTestCase.From("<reply>\n<servercmd>\nREPLY welcome 220 hi\n</servercmd>\n</reply>\n"),
            new NoListenPortConnector(new SwsHttpServerConnector(ParsedTestCase.From(string.Empty), TimeProvider.System)));
        MailTlsServerConnector connector = new(certificate, smtp);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", MailTlsServerConnector.SmtpsPort, false), TestContext.CancellationToken);
        string greeting;
        string reply;
        await using (SslStream client = await HandshakeAsync(result.Connection!))
        {
            greeting = await ReadAsync(client, 8);
            await client.WriteAsync(Encoding.Latin1.GetBytes("NOOP\r\n"), TestContext.CancellationToken);
            await client.FlushAsync(TestContext.CancellationToken);
            reply = await ReadAsync(client, 3);
        }

        Assert.AreEqual("220 hi\r\n", greeting);
        Assert.AreEqual("250", reply);
        Assert.AreEqual("NOOP\r\n", Encoding.Latin1.GetString(smtp.ProtocolLog.Span));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_ServerClosingTheConnection_EndsTheTlsStreamCleanly()
    {
        using X509Certificate2 certificate = CreateCertificate();
        MailTlsServerConnector connector = new(certificate, new LineProtocolServerConnector(() => new EchoResponder()));
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", MailTlsServerConnector.Pop3sPort, false), TestContext.CancellationToken);
        await using SslStream client = await HandshakeAsync(result.Connection!);

        await client.WriteAsync(Encoding.Latin1.GetBytes("QUIT\r\n"), TestContext.CancellationToken);
        await client.FlushAsync(TestContext.CancellationToken);
        using MemoryStream received = new();
        await client.CopyToAsync(received, TestContext.CancellationToken);
        await client.ShutdownAsync();

        Assert.AreEqual("hello\r\nbye\r\n", Encoding.Latin1.GetString(received.ToArray()));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_ClientSpeakingPlainText_IsDisconnectedUnrecorded()
    {
        using X509Certificate2 certificate = CreateCertificate();
        LineProtocolServerConnector plain = new(() => new EchoResponder());
        MailTlsServerConnector connector = new(certificate, plain);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", MailTlsServerConnector.Pop3sPort, false), TestContext.CancellationToken);
        await using IConnection connection = result.Connection!;

        await connection.WriteAsync(Encoding.Latin1.GetBytes("CAPA\r\n"), TestContext.CancellationToken);
        int read = await connection.ReadAsync(new byte[64], TestContext.CancellationToken);

        Assert.AreEqual(0, read);
        Assert.IsEmpty(plain.ReceivedBytes.ToArray());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task DisposeAsync_WhileTheServerWaitsForACommand_EndsTheRelay()
    {
        using X509Certificate2 certificate = CreateCertificate();
        LineProtocolServerConnector plain = new(() => new EchoResponder());
        MailTlsServerConnector connector = new(certificate, plain);
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", MailTlsServerConnector.ImapsPort, false), TestContext.CancellationToken);
        SslStream client = await HandshakeAsync(result.Connection!);
        await ReadAsync(client, 7);

        await client.DisposeAsync();

        Assert.IsEmpty(plain.ReceivedBytes.ToArray());
    }

    [TestMethod]
    public void EmulatedServers_AreTheThreeImplicitTlsMailServers()
    {
        CollectionAssert.AreEqual(new[] { "smtps", "imaps", "pop3s" }, MailTlsServerConnector.EmulatedServers.ToArray());
    }

    private static async Task<SslStream> HandshakeAsync(IConnection connection)
    {
        SslStream client = new(new ConnectionStream(connection), leaveInnerStreamOpen: false, static (_, _, _, _) => true);
        await client.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" });
        return client;
    }

    private static async Task<string> ReadAsync(SslStream client, int length)
    {
        byte[] reply = new byte[length];
        int count = 0;
        int read;
        while (count < length && (read = await client.ReadAsync(reply.AsMemory(count))) > 0)
        {
            count += read;
        }

        return Encoding.Latin1.GetString(reply, 0, count);
    }

    // A P-256 certificate reloaded through PKCS#12, since Windows Schannel and macOS reject a
    // server certificate whose key is ephemeral.
    private static X509Certificate2 CreateCertificate()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new("CN=localhost", key, HashAlgorithmName.SHA256);
        using X509Certificate2 ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null);
    }

    // Greets with "hello", answers QUIT with "bye" and a close, and every other line with "ok".
    private sealed class EchoResponder : ILineProtocolResponder
    {
        public ReadOnlyMemory<byte> Greeting => "hello\r\n"u8.ToArray();

        public LineProtocolReply Answer(string commandLine) =>
            commandLine == "QUIT" ? new("bye\r\n"u8.ToArray(), true) : new("ok\r\n"u8.ToArray(), false);
    }

    // Records the port of every connection it passes on.
    private sealed class RecordingConnector(IConnector inner) : IConnector
    {
        public List<int> Ports { get; } = [];

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Ports.Add(target.Port);
            return inner.ConnectAsync(target, cancellationToken);
        }
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
