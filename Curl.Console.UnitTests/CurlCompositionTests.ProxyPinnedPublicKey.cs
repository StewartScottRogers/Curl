using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

// Pins that --proxy-pinnedpubkey, --proxy-crlfile, --proxy-ca-native, --proxy-ssl-auto-client-cert and
// --proxy-ssl-allow-beast reach the handshake to an HTTPS proxy only, as their origin counterparts reach
// the origin's (BL-611). Measured with curl 8.21.0 (Schannel) against Record-CurlExchange.ps1 -Tls as the
// proxy, 2026-09-30: a wrong --proxy-pinnedpubkey is exit 90, the right one and a wrong --pinnedpubkey
// (the origin's, over plain HTTP) are exit 0.
public sealed partial class CurlCompositionTests
{
    private const string WrongProxyPin = "sha256//AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    [TestMethod]
    public async Task CreateTransports_WrongProxyPinnedpubkey_FailsTheProxysHandshakeWithExit90()
    {
        // curl -sS --proxy https://127.0.0.1:47611 --proxy-insecure --proxy-pinnedpubkey sha256//AAAA...= http://example.test/
        // curl: (90) SSL: public key does not match pinned public key (curl 8.21.0, 2026-09-30).
        ConnectResult result = await LoopbackProxyHandshakeAsync(_ => ["--proxy-pinnedpubkey", WrongProxyPin]);

        Assert.AreEqual(CurlExitCode.SslPinnedPubKeyNotMatch, result.ExitCode);
        Assert.AreEqual("SSL: public key does not match pinned public key", result.ErrorMessage);
    }

    [TestMethod]
    public async Task CreateTransports_TheProxysKeyPinned_CompletesTheProxysHandshake()
    {
        // curl ... --proxy-pinnedpubkey sha256//<the proxy's key> http://example.test/ -> exit 0.
        ConnectResult result = await LoopbackProxyHandshakeAsync(
            key => ["--proxy-pinnedpubkey", "sha256//" + Convert.ToBase64String(SHA256.HashData(key))]);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task CreateTransports_WrongOriginPinnedpubkey_LeavesTheProxysHandshakeAlone()
    {
        // curl ... --proxy-insecure --pinnedpubkey sha256//AAAA...= http://example.test/ -> exit 0.
        ConnectResult result = await LoopbackProxyHandshakeAsync(_ => ["--pinnedpubkey", WrongProxyPin]);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public void CreateTransports_ProxyPinnedpubkeyAndProxyCrlfile_ReachTheProxysOptionsOnly()
    {
        CurlTransports transports = CurlComposition.CreateTransports(
            Parse("-x", HttpsProxyUrl, "--proxy-pinnedpubkey", WrongProxyPin, "--proxy-crlfile", "proxy.crl", ProxiedUrl));

        Assert.AreEqual(WrongProxyPin, transports.ProxyTlsClientOptions.PinnedPublicKey);
        Assert.AreEqual("proxy.crl", transports.ProxyTlsClientOptions.CertificateRevocationListFile);
        Assert.IsNull(transports.TlsClientOptions.PinnedPublicKey);
        Assert.IsNull(transports.TlsClientOptions.CertificateRevocationListFile);
    }

    [TestMethod]
    public void CreateTransports_ProxyCaNativeAndProxySslAllowBeast_ChangeNeitherHandshake()
    {
        // curl ... --proxy-ca-native (without --proxy-insecure) against a self-signed proxy is exit 60, as
        // without it; --proxy-ssl-allow-beast is exit 0 (curl 8.21.0, 2026-09-30).
        CurlTransports without = CurlComposition.CreateTransports(Parse("-x", HttpsProxyUrl, ProxiedUrl));
        CurlTransports with = CurlComposition.CreateTransports(
            Parse("-x", HttpsProxyUrl, "--proxy-ca-native", "--proxy-ssl-allow-beast", ProxiedUrl));

        Assert.AreEqual(without.ProxyTlsClientOptions, with.ProxyTlsClientOptions);
        Assert.AreEqual(without.TlsClientOptions, with.TlsClientOptions);
    }

    // Runs the HTTPS proxy's handshake against a loopback TLS server with a throwaway self-signed key.
    private static async Task<ConnectResult> LoopbackProxyHandshakeAsync(Func<byte[], string[]> proxyTlsArguments)
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=127.0.0.1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null);
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        Task serverTask = ServeOneHandshakeAsync(listener, certificate);

        CurlTransports transports = CurlComposition.CreateTransports(
            Parse(["-x", HttpsProxyUrl, "--proxy-insecure", .. proxyTlsArguments(key.ExportSubjectPublicKeyInfo()), ProxiedUrl]));
        TcpClient client = new();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
        ConnectResult result = await transports.ProxyTlsProvider.AuthenticateAsClientAsync(
            new StreamConnection(client.GetStream(), listener.LocalEndpoint), "127.0.0.1", CancellationToken.None);

        if (result.Connection is null)
        {
            client.Dispose();
        }

        try
        {
            await serverTask;
        }
        catch (Exception exception) when (exception is IOException or AuthenticationException or SocketException)
        {
            // The client closed the connection on a failed pin.
        }

        return result;
    }

    private static async Task ServeOneHandshakeAsync(TcpListener listener, X509Certificate2 certificate)
    {
        using TcpClient accepted = await listener.AcceptTcpClientAsync();
        await using SslStream server = new(accepted.GetStream());
        await server.AuthenticateAsServerAsync(certificate);
    }
}
