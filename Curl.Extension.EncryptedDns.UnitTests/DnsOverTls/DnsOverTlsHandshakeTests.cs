using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Curl.Extension.Abstractions;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverTls;

/// <summary>
/// Resolves through a test-double DNS-over-TLS server whose certificate chains to a private CA, over an in-memory
/// connection, with the TLS options <c>--dns-over-tls</c> takes from the transfer's <c>--cacert</c>, <c>--ssl-no-revoke</c> (the private CA publishes no revocation list, which the Schannel build would otherwise check) and <c>-k</c>
/// (BL-5872, ADR-1353): the lookup succeeds with the CA or <c>-k</c> and finds nothing without either.
/// </summary>
[TestClass]
public sealed class DnsOverTlsHandshakeTests
{
    private const string ServerName = "dot.example";

    private static X509Certificate2 s_serverCertificate = null!;
    private static string s_directory = null!;
    private static string s_caFile = null!;

    [ClassInitialize]
    public static void CreatePrivateCa(TestContext context)
    {
        using var caKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var caRequest = new CertificateRequest("CN=DoT Test CA", caKey, HashAlgorithmName.SHA256);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));

        using var serverKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var serverRequest = new CertificateRequest("CN=" + ServerName, serverKey, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(ServerName);
        serverRequest.CertificateExtensions.Add(names.Build());
        serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var issued = serverRequest.Create(ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1), [1, 2, 3, 4]);
        using var withKey = issued.CopyWithPrivateKey(serverKey);
        s_serverCertificate = X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pkcs12), null);

        s_directory = Directory.CreateTempSubdirectory("dot-ca-").FullName;
        s_caFile = Path.Combine(s_directory, "ca.pem");
        File.WriteAllText(s_caFile, ca.ExportCertificatePem());
    }

    [ClassCleanup]
    public static void DeletePrivateCa()
    {
        s_serverCertificate.Dispose();
        Directory.Delete(s_directory, recursive: true);
    }

    [TestMethod]
    public async Task ResolveAsync_CaCertificateOfThePrivateCaAndSslNoRevoke_ResolvesTheName()
    {
        var addresses = await ResolveThroughTestServerAsync(new ExtensionTlsTrust(CaCertificateFile: s_caFile, SkipRevocationCheck: true));

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.1") }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_InsecureWithAnUntrustedCertificate_ResolvesTheName()
    {
        var addresses = await ResolveThroughTestServerAsync(new ExtensionTlsTrust(Insecure: true));

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("192.0.2.1") }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_NeitherCaCertificateNorInsecure_RejectsTheCertificateAndFindsNothing()
    {
        var addresses = await ResolveThroughTestServerAsync(new ExtensionTlsTrust());

        Assert.IsEmpty(addresses);
    }

    private static async Task<IReadOnlyList<IPAddress>> ResolveThroughTestServerAsync(ExtensionTlsTrust trust)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var values = new ExtensionOptionValues(new Dictionary<string, string> { ["dns-over-tls"] = ServerName }, trust);
        var hook = (DnsOverTlsResolverHook)EncryptedDnsExtension.ActivateDnsOverTls(values)!;
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = AnswerOneLookupAsync(server, timeout.Token);

        var resolver = new DnsOverTlsResolver(new StreamConnector(client), new SslStreamTlsProvider(hook.TlsOptions, TimeProvider.System), hook.Server);
        var addresses = await resolver.ResolveAsync("api.example.test", timeout.Token);

        client.Dispose();
        await serverTask;
        return addresses;
    }

    // Completes the handshake and answers the AAAA query with no address and the A query with 192.0.2.1; a client
    // that rejects the certificate ends the handshake, which the server shrugs off.
    private static async Task AnswerOneLookupAsync(InMemoryDuplexStream server, CancellationToken cancellationToken)
    {
        await using var tls = new SslStream(server);
        try
        {
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = s_serverCertificate }, cancellationToken);
            byte[] aaaa = [0x00, 0x02, 0x81, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
            byte[] a = [0x00, 0x01, 0x81, 0x80, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x04, 192, 0, 2, 1];
            await tls.WriteAsync((byte[])[0, (byte)aaaa.Length, .. aaaa, 0, (byte)a.Length, .. a], cancellationToken);
            await tls.FlushAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is AuthenticationException or IOException)
        {
        }
    }

    private sealed class StreamConnector(Stream stream) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Connected(new StreamConnection(stream, new DnsEndPoint(target.Host, target.Port))));
    }

    /// <summary>One end of an in-memory connection: bytes written here are read at the other end, and disposing either end makes the other read end of stream.</summary>
    private sealed class InMemoryDuplexStream(Channel<byte[]> incoming, Channel<byte[]> outgoing) : Stream
    {
        private byte[] _pending = [];
        private int _offset;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public static (InMemoryDuplexStream Client, InMemoryDuplexStream Server) CreatePair()
        {
            var toServer = Channel.CreateUnbounded<byte[]>();
            var toClient = Channel.CreateUnbounded<byte[]>();
            return (new InMemoryDuplexStream(toClient, toServer), new InMemoryDuplexStream(toServer, toClient));
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_offset == _pending.Length)
            {
                if (!await incoming.Reader.WaitToReadAsync(cancellationToken) || !incoming.Reader.TryRead(out byte[]? next))
                {
                    return 0;
                }

                (_pending, _offset) = (next, 0);
            }

            int count = Math.Min(buffer.Length, _pending.Length - _offset);
            _pending.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            outgoing.Writer.TryWrite(buffer.ToArray());
            return ValueTask.CompletedTask;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            outgoing.Writer.TryComplete();
            incoming.Writer.TryComplete();
            base.Dispose(disposing);
        }
    }
}
