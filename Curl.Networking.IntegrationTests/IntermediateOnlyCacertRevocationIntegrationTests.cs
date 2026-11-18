using System.Formats.Asn1;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins an intermediate-only <c>--cacert</c> / <c>--proxy-cacert</c> with revocation on against a real chain
/// whose CRL is served by a real loopback listener and fetched by the production downloader, with no stubbed
/// chain statuses (BL-5385).
/// </summary>
[TestClass]
public sealed class IntermediateOnlyCacertRevocationIntegrationTests
{
    private const string Host = "localhost";

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-int-cacert-revocation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_RealChainWithTheCrlServedOverLoopback_Accepts()
    {
        using var pki = new Pki(revokeLeaf: false, excludeHostName: false);
        using var server = new CrlServer(pki.CrlBytes);
        pki.IssueLeaf(server.Address);

        Assert.IsNull(VerifyRealChain(pki, new TlsClientOptions(CaCertificateFile: WriteCaFile(pki))));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_RealChainWithTheLeafRevokedOnTheServedCrl_IsExit60WithTheRevokedText()
    {
        using var pki = new Pki(revokeLeaf: true, excludeHostName: false);
        using var server = new CrlServer(pki.CrlBytes);
        pki.IssueLeaf(server.Address);

        var failure = VerifyRealChain(pki, new TlsClientOptions(CaCertificateFile: WriteCaFile(pki)));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_RealChainWithRevokeBestEffort_AcceptsTheSameChain()
    {
        using var pki = new Pki(revokeLeaf: false, excludeHostName: false);
        using var server = new CrlServer(pki.CrlBytes);
        pki.IssueLeaf(server.Address);

        Assert.IsNull(VerifyRealChain(pki, new TlsClientOptions(CaCertificateFile: WriteCaFile(pki), RevocationCheckBestEffort: true)));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_RealChainWithTheHostNameExcludedByTheAnchor_IsRefused()
    {
        using var pki = new Pki(revokeLeaf: false, excludeHostName: true);
        using var server = new CrlServer(pki.CrlBytes);
        pki.IssueLeaf(server.Address);

        var failure = VerifyRealChain(pki, new TlsClientOptions(CaCertificateFile: WriteCaFile(pki)));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    private string WriteCaFile(Pki pki)
    {
        var caFile = Path.Combine(_directory, "int.pem");
        File.WriteAllText(caFile, pki.Intermediate.ExportCertificatePem());
        return caFile;
    }

    private static (CurlExitCode ExitCode, string Message)? VerifyRealChain(Pki pki, TlsClientOptions options)
    {
        // The proxy policy (--proxy-cacert) builds the same verification with the proxy's options.
        var verification = new ServerCertificateVerification(options, true, TimeProvider.System);
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.ExtraStore.Add(pki.Intermediate);
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(pki.Leaf);
        var errors = chain.ChainStatus.Length == 0 ? SslPolicyErrors.None : SslPolicyErrors.RemoteCertificateChainErrors;
        return verification.VerifyPeer(errors, chain, Host, []);
    }

    // Serves one CRL to every request on a loopback port, as a CRL distribution point would.
    private sealed class CrlServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        private readonly byte[] _crl;

        public CrlServer(byte[] crl)
        {
            _crl = crl;
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        public string Address => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/int.crl";

        public void Dispose() => _listener.Stop();

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    var stream = client.GetStream();
                    var buffer = new byte[4096];
                    _ = await stream.ReadAsync(buffer);
                    var head = System.Text.Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 200 OK\r\nContent-Type: application/pkix-crl\r\nContent-Length: {_crl.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(head);
                    await stream.WriteAsync(_crl);
                }
            }
            catch (Exception exception) when (exception is ObjectDisposedException or SocketException or IOException or InvalidOperationException)
            {
                // The listener was stopped: the test is over.
            }
        }
    }

    // root -> intermediate (the --cacert file) -> leaf with a distribution point on the loopback server.
    private sealed class Pki : IDisposable
    {
        private static readonly byte[] LeafSerial = [0x42, 0x17];

        private readonly X509Certificate2 _root;

        private readonly RSA _intermediateKey;

        public Pki(bool revokeLeaf, bool excludeHostName)
        {
            _root = CreateAuthority("CN=Live Root", null, out _, excludeHostName: false);
            using var intermediate = CreateAuthority("CN=Live Intermediate", _root, out _intermediateKey, excludeHostName);
            Intermediate = intermediate.CopyWithPrivateKey(_intermediateKey);
            var builder = new CertificateRevocationListBuilder();
            if (revokeLeaf)
            {
                builder.AddEntry(LeafSerial, DateTimeOffset.UtcNow.AddHours(-1), X509RevocationReason.KeyCompromise);
            }

            var nextUpdate = DateTimeOffset.UtcNow.AddDays(3);
            CrlBytes = builder.Build(Intermediate, 1, nextUpdate, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, nextUpdate.AddDays(-4));
        }

        public X509Certificate2 Intermediate { get; }

        public X509Certificate2 Leaf { get; private set; } = null!;

        public byte[] CrlBytes { get; }

        public void IssueLeaf(string crlAddress)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(Host);
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
            request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(Intermediate, true, false));
            request.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([crlAddress]));
            Leaf = request.Create(Intermediate, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), LeafSerial);
        }

        public void Dispose()
        {
            Leaf?.Dispose();
            Intermediate.Dispose();
            _intermediateKey.Dispose();
            _root.Dispose();
        }

        private static X509Certificate2 CreateAuthority(string subject, X509Certificate2? issuer, out RSA key, bool excludeHostName)
        {
            key = RSA.Create(2048);
            var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            if (excludeHostName)
            {
                request.CertificateExtensions.Add(new X509Extension(new Oid("2.5.29.30"), ExcludeHostNameConstraint(), true));
            }

            if (issuer is null)
            {
                return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
            }

            request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
            return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [1, 2, 3]);
        }

        // NameConstraints ::= SEQUENCE { excludedSubtrees [1] { GeneralSubtree { dNSName "localhost" } } }
        private static byte[] ExcludeHostNameConstraint()
        {
            var writer = new AsnWriter(AsnEncodingRules.DER);
            using (writer.PushSequence())
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1)))
            using (writer.PushSequence())
            {
                writer.WriteCharacterString(UniversalTagNumber.IA5String, Host, new Asn1Tag(TagClass.ContextSpecific, 2));
            }

            return writer.Encode();
        }
    }
}
