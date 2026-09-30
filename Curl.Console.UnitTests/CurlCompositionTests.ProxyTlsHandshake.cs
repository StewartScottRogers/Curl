using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Cli;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

// Pins that --proxy-cert, --proxy-pass and --proxy-ciphers fail the handshake to an HTTPS proxy as
// --cert, --pass and --ciphers fail the origin's, with the same exit code and message (BL-606). Each
// failure comes before a byte is read or written, so the connection handed to the proxy's TLS
// provider is never used.
public sealed partial class CurlCompositionTests
{
    private const string HttpsProxyUrl = "https://127.0.0.1:47606";

    private const string ProxiedUrl = "http://example.com/";

    private static string s_directory = string.Empty;

    private static string s_clientCertificateFile = string.Empty;

    /// <summary>Writes a PKCS#12 client certificate protected by the password <c>right</c>.</summary>
    [ClassInitialize]
    public static void WriteClientCertificate(TestContext context)
    {
        _ = context;
        s_directory = Directory.CreateTempSubdirectory("bl606-").FullName;
        s_clientCertificateFile = Path.Combine(s_directory, "client.p12");
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=client", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
        File.WriteAllBytes(s_clientCertificateFile, certificate.Export(X509ContentType.Pfx, "right"));
    }

    /// <summary>Deletes the client certificate.</summary>
    [ClassCleanup]
    public static void DeleteClientCertificate() => Directory.Delete(s_directory, recursive: true);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CreateTransports_MissingProxyCert_FailsAsCurlSchannelDoes()
    {
        // curl -sS -x https://127.0.0.1:47606 --proxy-insecure --proxy-cert missing.pem http://example.com/
        // curl: (58) schannel: Failed to get certificate location or file for missing.pem (curl 8.21.0, 2026-09-30).
        ConnectResult result = await ProxyHandshakeAsync("--proxy-cert", "missing.pem");

        Assert.AreEqual(CurlExitCode.SslCertProblem, result.ExitCode);
        Assert.AreEqual("schannel: Failed to get certificate location or file for missing.pem", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CreateTransports_WrongProxyPass_FailsAsCurlSchannelDoes()
    {
        // curl -sS -x https://127.0.0.1:47606 --proxy-insecure --proxy-cert client.p12 --proxy-cert-type P12
        // --proxy-pass wrong http://example.com/
        // curl: (58) schannel: Failed to import cert file <file>, password is bad (curl 8.21.0, 2026-09-30).
        ConnectResult result = await ProxyHandshakeAsync("--proxy-cert", s_clientCertificateFile, "--proxy-cert-type", "P12", "--proxy-pass", "wrong");

        Assert.AreEqual(CurlExitCode.SslCertProblem, result.ExitCode);
        Assert.AreEqual($"schannel: Failed to import cert file {s_clientCertificateFile}, password is bad", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task CreateTransports_ProxyCiphers_FailsAsCurlSchannelDoes()
    {
        // curl -sS -x https://127.0.0.1:47606 --proxy-insecure --proxy-ciphers BOGUS http://example.com/
        // curl: (59) schannel: Failed setting algorithm cipher list (curl 8.21.0, 2026-09-30).
        ConnectResult result = await ProxyHandshakeAsync("--proxy-ciphers", "BOGUS");

        Assert.AreEqual(CurlExitCode.SslCipher, result.ExitCode);
        Assert.AreEqual("schannel: Failed setting algorithm cipher list", result.ErrorMessage);
    }

    [TestMethod]
    public async Task CreateTransports_MissingProxyCert_FailsAsTheOriginsMissingCertDoes()
    {
        await AssertProxyFailsAsOriginAsync(["--proxy-cert", "missing.pem"], ["--cert", "missing.pem"]);
    }

    [TestMethod]
    public async Task CreateTransports_WrongProxyPass_FailsAsTheOriginsWrongPassDoes()
    {
        await AssertProxyFailsAsOriginAsync(
            ["--proxy-cert", s_clientCertificateFile, "--proxy-cert-type", "P12", "--proxy-pass", "wrong"],
            ["--cert", s_clientCertificateFile, "--cert-type", "P12", "--pass", "wrong"]);
    }

    [TestMethod]
    public async Task CreateTransports_ProxyCiphers_FailsAsTheOriginsCiphersDo()
    {
        await AssertProxyFailsAsOriginAsync(["--proxy-ciphers", "BOGUS"], ["--ciphers", "BOGUS"]);
    }

    [TestMethod]
    public async Task CreateTransports_ProxyCertAndProxyCiphers_LeaveTheOriginsHandshakeAlone()
    {
        CurlTransports transports = CurlComposition.CreateTransports(
            Parse("-x", HttpsProxyUrl, "--proxy-cert", "missing.pem", "--proxy-ciphers", "BOGUS", ProxiedUrl));
        UnusedConnection connection = new();

        ConnectResult result = await transports.TlsProvider.AuthenticateAsClientAsync(connection, "example.com", CancellationToken.None);

        Assert.IsTrue(connection.WasRead, "The origin's handshake reached the network instead of failing on a proxy option.");
        Assert.AreNotEqual(CurlExitCode.SslCertProblem, result.ExitCode);
        Assert.AreNotEqual(CurlExitCode.SslCipher, result.ExitCode);
    }

    private static async Task AssertProxyFailsAsOriginAsync(string[] proxyArguments, string[] originArguments)
    {
        ConnectResult proxy = await ProxyHandshakeAsync(proxyArguments);
        CurlTransports originTransports = CurlComposition.CreateTransports(Parse([.. originArguments, "https://127.0.0.1:47606/"]));
        ConnectResult origin = await originTransports.TlsProvider.AuthenticateAsClientAsync(new UnusedConnection(), "127.0.0.1", CancellationToken.None);

        Assert.AreNotEqual(CurlExitCode.Ok, proxy.ExitCode);
        Assert.AreEqual(origin.ExitCode, proxy.ExitCode);
        Assert.AreEqual(origin.ErrorMessage, proxy.ErrorMessage);
    }

    private static async Task<ConnectResult> ProxyHandshakeAsync(params string[] proxyTlsArguments)
    {
        CurlTransports transports = CurlComposition.CreateTransports(Parse(["-x", HttpsProxyUrl, "--proxy-insecure", .. proxyTlsArguments, ProxiedUrl]));
        return await transports.ProxyTlsProvider.AuthenticateAsClientAsync(new UnusedConnection(), "127.0.0.1", CancellationToken.None);
    }

    // A connection whose peer closes at once: a handshake that gets as far as reading fails there.
    private sealed class UnusedConnection : IConnection
    {
        public bool WasRead { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            WasRead = true;
            return ValueTask.FromResult(0);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
