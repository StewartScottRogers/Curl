using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins the KDC proxy's own TLS (ADR-0300, BL-1063): real handshakes with
/// <see cref="KerberosKdcProxyTlsClient" /> against a server-side <see cref="SslStream" /> over
/// <see cref="InMemoryDuplexStream" />, reached through a connector that plays a transfer run
/// with <c>-k</c>. A certificate under the realm's <c>http_anchors</c> is accepted; one that is
/// not, or one the system does not trust when there are no anchors, fails with an
/// <see cref="IOException" /> whatever the transfer's <c>-k</c>; an anchor that cannot be loaded
/// fails before any connection.
/// </summary>
public sealed partial class KerberosKdcProxyHttpsTransportTests
{
    private const string ProxyHost = "localhost";

    private static readonly Lazy<X509Certificate2> ProxyCertificate = new(() => SelfSigned(ProxyHost));

    private static readonly Lazy<X509Certificate2> OtherCertificate = new(() => SelfSigned("other.example.test"));

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PostAsync_ProxyCertificateUnderTheAnchors_ReturnsTheBody()
    {
        string anchorFile = WritePem(ProxyCertificate.Value);
        InsecureTransferConnector connector = new(ProxyCertificate.Value);

        byte[] reply = await PostAnchoredAsync(AnchoredTransport(connector), [$"FILE:{anchorFile}"], new byte[] { 0x30 }, TestContext.CancellationToken);

        Diagnostics.Diff("body", new byte[] { 0xAA, 0xBB }, reply);
        Diagnostics.Assert("request line", "POST /KdcProxy HTTP/1.0", connector.ReceivedRequestLine);
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, reply);
        Assert.AreEqual("POST /KdcProxy HTTP/1.0", connector.ReceivedRequestLine);
    }

    [TestMethod]
    public async Task PostAsync_ProxyCertificateNotUnderTheAnchors_ThrowsIOExceptionEvenWhenTheTransferRunsWithMinusK()
    {
        string anchorFile = WritePem(OtherCertificate.Value);
        InsecureTransferConnector connector = new(ProxyCertificate.Value);

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(
            () => PostAnchoredAsync(AnchoredTransport(connector), [$"FILE:{anchorFile}"], new byte[] { 0x30 }, TestContext.CancellationToken));

        Diagnostics.Assert("message starts with \"The KDC proxy localhost sent a certificate that does not verify: \"", true, failure.Message.StartsWith("The KDC proxy localhost sent a certificate that does not verify: ", StringComparison.Ordinal));
        Diagnostics.Assert("message names RemoteCertificateChainErrors", true, failure.Message.Contains(nameof(SslPolicyErrors.RemoteCertificateChainErrors), StringComparison.Ordinal));
        Diagnostics.Assert("transfer TLS asked for", false, connector.Targets.Single().UseTls);
        Diagnostics.Assert("request line", "null", connector.ReceivedRequestLine ?? "null");
        StringAssert.StartsWith(failure.Message, "The KDC proxy localhost sent a certificate that does not verify: ");
        StringAssert.Contains(failure.Message, nameof(SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.IsFalse(connector.Targets.Single().UseTls, "The transfer's -k TLS is never asked for.");
        Assert.IsNull(connector.ReceivedRequestLine);
    }

    [TestMethod]
    public async Task PostAsync_NoAnchorsAndAProxyTheSystemDoesNotTrust_ThrowsIOExceptionEvenWhenTheTransferRunsWithMinusK()
    {
        InsecureTransferConnector connector = new(ProxyCertificate.Value);

        await Assert.ThrowsExactlyAsync<IOException>(
            () => PostAnchoredAsync(AnchoredTransport(connector), [], new byte[] { 0x30 }, TestContext.CancellationToken));

        Diagnostics.Assert("request line", "null", connector.ReceivedRequestLine ?? "null");
        Assert.IsNull(connector.ReceivedRequestLine);
    }

    [TestMethod]
    public async Task PostAsync_AnAnchorThatCannotBeLoaded_ThrowsIOExceptionWithoutConnecting()
    {
        FakeConnector connector = new();
        KerberosKdcProxyHttpsTransport transport = new(connector, TimeSpan.FromSeconds(10), TimeProvider.System, new KerberosKdcProxyTlsClient(), _ => null);

        IOException failure = await Assert.ThrowsExactlyAsync<IOException>(
            () => PostAnchoredAsync(transport, ["ENV:KDCPROXY_CA"], new byte[] { 0x30 }, TestContext.CancellationToken));

        Diagnostics.Diff("message", "The http_anchors environment variable KDCPROXY_CA is not set.", failure.Message);
        Diagnostics.Assert("connections opened", 0, connector.Targets.Count);
        Assert.AreEqual("The http_anchors environment variable KDCPROXY_CA is not set.", failure.Message);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task AuthenticateAsync_Cancelled_ThrowsOperationCanceledAndDisposesTheStream()
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        Diagnostics.Arrange("host", ProxyHost);
        Diagnostics.Arrange("cancellation", "cancelled before the handshake");

        var failure = await Assert.ThrowsAsync<OperationCanceledException>(
            () => new KerberosKdcProxyTlsClient().AuthenticateAsync(client, ProxyHost, null, cancelled.Token));

        Diagnostics.Act("exception", failure.GetType().Name);
        Diagnostics.Assert("stream disposed", true, client.IsDisposed);
        Assert.IsTrue(client.IsDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsync_PeerClosesBeforeTheHandshake_ThrowsIOExceptionAndDisposesTheStream()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await server.DisposeAsync();
        Diagnostics.Arrange("host", ProxyHost);
        Diagnostics.Arrange("peer", "closed before the handshake");

        IOException failure;
        using (Diagnostics.Phase("handshake"))
        {
            failure = await Assert.ThrowsExactlyAsync<IOException>(
                () => new KerberosKdcProxyTlsClient().AuthenticateAsync(client, ProxyHost, null, TestContext.CancellationToken));
        }

        Diagnostics.Act("exception", failure.GetType().Name);
        Diagnostics.Assert("message starts with \"The KDC proxy localhost failed the TLS handshake: \"", true, failure.Message.StartsWith("The KDC proxy localhost failed the TLS handshake: ", StringComparison.Ordinal));
        Diagnostics.Assert("stream disposed", true, client.IsDisposed);
        StringAssert.StartsWith(failure.Message, "The KDC proxy localhost failed the TLS handshake: ");
        Assert.IsTrue(client.IsDisposed);
    }

    // The anchor file's path differs by platform and run, so the lines name only the anchor kind;
    // a failed handshake's words are the platform's own, so they show only the exception's type.
    private async Task<byte[]> PostAnchoredAsync(KerberosKdcProxyHttpsTransport transport, IReadOnlyList<string> httpAnchors, byte[] body, CancellationToken cancellationToken)
    {
        Diagnostics.Arrange("proxy", $"{ProxyHost} port 443");
        Diagnostics.Arrange("http_anchors", httpAnchors.Count == 0 ? "none" : string.Join(", ", httpAnchors.Select(anchor => anchor.StartsWith("FILE:", StringComparison.Ordinal) ? "FILE:<anchor file>" : anchor)));
        Diagnostics.Bytes("KDC request", body);
        try
        {
            byte[] reply;
            using (Diagnostics.Phase("post with TLS handshake"))
            {
                reply = await transport.PostAsync(ProxyHost, 443, "KdcProxy", httpAnchors, body, cancellationToken);
            }

            Diagnostics.Bytes("reply body", reply);
            Diagnostics.Act("reply body length", reply.Length);
            return reply;
        }
        catch (Exception exception) when (WriteFailureType(exception))
        {
            throw;
        }
    }

    private bool WriteFailureType(Exception exception)
    {
        Diagnostics.Act("exception", exception.GetType().Name);
        return false;
    }

    private static KerberosKdcProxyHttpsTransport AnchoredTransport(IConnector connector) =>
        new(connector, TimeSpan.FromSeconds(30), TimeProvider.System, new KerberosKdcProxyTlsClient(), _ => null);

    private static X509Certificate2 SelfSigned(string host)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new($"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        SubjectAlternativeNameBuilder names = new();
        names.AddDnsName(host);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        using X509Certificate2 ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }

    private string WritePem(X509Certificate2 certificate)
    {
        string path = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), $"kdcproxy-anchor-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, certificate.ExportCertificatePem());
        return path;
    }

    /// <summary>
    /// Plays the run's connector of a transfer with <c>-k</c>: a target asked for TLS would get
    /// the insecure <see cref="SslStreamTlsProvider" />'s handshake; every connection reaches an
    /// in-memory HTTPS proxy presenting <paramref name="serverCertificate" /> that answers <c>200</c>.
    /// </summary>
    private sealed class InsecureTransferConnector(X509Certificate2 serverCertificate) : IConnector
    {
        public List<ConnectTarget> Targets { get; } = [];

        public string? ReceivedRequestLine { get; private set; }

        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            Targets.Add(target);
            var (client, server) = InMemoryDuplexStream.CreatePair();
            _ = ServeAsync(server);
            StreamConnection connection = new(client, new IPEndPoint(IPAddress.Loopback, target.Port));
            return target.UseTls
                ? await new SslStreamTlsProvider(new TlsClientOptions(Insecure: true)).AuthenticateAsClientAsync(connection, target.Host, cancellationToken)
                : ConnectResult.Connected(connection);
        }

        private async Task ServeAsync(InMemoryDuplexStream server)
        {
            await using SslStream secured = new(server, leaveInnerStreamOpen: false);
            try
            {
                await secured.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = serverCertificate });
                byte[] buffer = new byte[4096];
                int read = await secured.ReadAsync(buffer);
                string request = Encoding.ASCII.GetString(buffer, 0, read);
                ReceivedRequestLine = request[..request.IndexOf("\r\n", StringComparison.Ordinal)];
                await secured.WriteAsync("HTTP/1.1 200 OK\r\n\r\n"u8.ToArray().Concat(new byte[] { 0xAA, 0xBB }).ToArray());
                await secured.FlushAsync();
            }
            catch (Exception exception) when (exception is IOException or System.Security.Authentication.AuthenticationException or ObjectDisposedException)
            {
                // The client refused the certificate and closed its end.
            }
        }
    }
}
