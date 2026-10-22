using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

// The https-mtls server (BL-2859): HttpsServerConnector on HttpsMtlsPort with a TlsClientCertificateVerifier over the
// test CA demands a client certificate, as upstream's stunnel at verify = 2 does for test2088 and test2089.
public sealed partial class HttpsServerConnectorTests
{
    private const string ExpectedReply = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi";

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_MtlsPortWithTheEkuOnlyClientCertificate_SucceedsAndRecordsIt()
    {
        string certificateDirectory = GenerateMtlsCertificates();
        using X509Certificate2 clientCertificate = LoadClientCertificate(certificateDirectory, "test-client-eku-only");
        (HttpsServerConnector connector, TlsClientCertificateVerifier verifier) = MtlsServer(certificateDirectory);

        string? reply = await TryExchangeAsync(connector, clientCertificate);

        Assert.AreEqual(ExpectedReply, reply);
        Assert.HasCount(1, verifier.ReceivedCertificates);
        Assert.AreEqual(clientCertificate.Thumbprint, verifier.ReceivedCertificates[0].Thumbprint);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_MtlsPortWithoutAClientCertificate_FailsTheHandshake()
    {
        string certificateDirectory = GenerateMtlsCertificates();
        (HttpsServerConnector connector, TlsClientCertificateVerifier verifier) = MtlsServer(certificateDirectory);

        string? reply = await TryExchangeAsync(connector, clientCertificate: null);

        Assert.AreNotEqual(ExpectedReply, reply);
        Assert.IsEmpty(verifier.ReceivedCertificates);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectAsync_MtlsPortWithACertificateTheTestCaDidNotSign_FailsTheHandshakeAndRecordsIt()
    {
        string certificateDirectory = GenerateMtlsCertificates();
        using X509Certificate2 selfSigned = CreateCertificate();
        (HttpsServerConnector connector, TlsClientCertificateVerifier verifier) = MtlsServer(certificateDirectory);

        string? reply = await TryExchangeAsync(connector, selfSigned);

        Assert.AreNotEqual(ExpectedReply, reply);
        Assert.HasCount(1, verifier.ReceivedCertificates);
    }

    [TestMethod]
    public async Task ConnectAsync_MtlsServerAndTheHttpsPort_ReachesTheWrappedConnector()
    {
        using X509Certificate2 certificate = CreateCertificate();
        SwsHttpServerConnector sws = new(ParsedTestCase.From(KeptOpenReply));
        HttpsServerConnector connector = new(sws, certificate, new NoListenPortConnector(sws), HttpsServerConnector.HttpsMtlsPort, new TlsClientCertificateVerifier(certificate));

        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", NoListenPortConnector.NoListenPort, false), CancellationToken.None);

        Assert.IsTrue(result.IsConnectionRefused);
    }

    private static (HttpsServerConnector Connector, TlsClientCertificateVerifier Verifier) MtlsServer(string certificateDirectory)
    {
        string certs = Path.Combine(certificateDirectory, "certs");
        TlsClientCertificateVerifier verifier = new(X509CertificateLoader.LoadCertificateFromFile(Path.Combine(certs, "test-ca.crt")));
        SwsHttpServerConnector sws = new(ParsedTestCase.From(KeptOpenReply));
        X509Certificate2 serverCertificate = HttpsServerConnector.LoadCertificate(Path.Combine(certs, HttpsServerConnector.DefaultCertificateFile));
        return (new HttpsServerConnector(sws, serverCertificate, sws, HttpsServerConnector.HttpsMtlsPort, verifier), verifier);
    }

    // The reply to one request over a TLS connection to the mTLS port, or null when the handshake or the exchange fails.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure of a refused handshake is the outcome under test.")]
    private static async Task<string?> TryExchangeAsync(HttpsServerConnector connector, X509Certificate2? clientCertificate)
    {
        ConnectResult result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", HttpsServerConnector.HttpsMtlsPort, false), CancellationToken.None);
        SslStream client = new(new ConnectionStream(result.Connection!), leaveInnerStreamOpen: false, static (_, _, _, _) => true);
        await using (client)
        {
            try
            {
                await client.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    LocalCertificateSelectionCallback = (_, _, _, _, _) => clientCertificate!,
                });
                return await ExchangeAsync(client, ExpectedReply.Length);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    // The client certificate from its .crt and .key, reloaded through PKCS#12, since Schannel rejects an ephemeral client key.
    private static X509Certificate2 LoadClientCertificate(string certificateDirectory, string name)
    {
        string certs = Path.Combine(certificateDirectory, "certs");
        using X509Certificate2 ephemeral = X509Certificate2.CreateFromPemFile(Path.Combine(certs, name + ".crt"), Path.Combine(certs, name + ".key"));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null);
    }

    private static string GenerateMtlsCertificates() => MtlsTestCertificates.CertificateDirectory;
}
