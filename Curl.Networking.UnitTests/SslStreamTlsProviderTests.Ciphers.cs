using System.Net.Security;
using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="SslStreamTlsProvider" /> with <c>--ciphers</c> and <c>--tls13-ciphers</c>
/// (<see cref="TlsClientOptions.Ciphers" />, <see cref="TlsClientOptions.Tls13Ciphers" />),
/// as ADR-0011 decides from curl 8.21.0's Schannel build and the OpenSSL build measured
/// 2026-09-26: the Schannel build refuses <c>--ciphers</c> with exit 59 and ignores
/// <c>--tls13-ciphers</c>; the OpenSSL build offers the suites named, which the server
/// here then negotiates, and fails with exit 59 when a list names none.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    [DataRow("BOGUS")]
    [DataRow("ECDHE-RSA-AES128-GCM-SHA256")]
    [DataRow("TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256")]
    public async Task AuthenticateAsClientAsync_WithCiphersInTheSchannelBuild_FailsWithSslCipherAsSchannelCurlDoes(string ciphers)
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, Ciphers: ciphers), CertificateHost, SslProtocols.None, SchannelBuild);

        Assert.AreEqual(CurlExitCode.SslCipher, result.Result.ExitCode);
        Assert.AreEqual("schannel: Failed setting algorithm cipher list", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    [DataRow("BOGUS")]
    [DataRow("TLS_AES_128_GCM_SHA256")]
    public async Task AuthenticateAsClientAsync_WithTls13CiphersInTheSchannelBuild_IgnoresThemAndConnects(string tls13Ciphers)
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, Tls13Ciphers: tls13Ciphers), CertificateHost, SslProtocols.None, SchannelBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithUnknownCiphersInTheOpenSslBuild_FailsWithSslCipherNamingTheList()
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, Ciphers: "BOGUS"), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.SslCipher, result.Result.ExitCode);
        Assert.AreEqual("failed setting cipher list: BOGUS", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithUnknownTls13CiphersInTheOpenSslBuild_FailsWithSslCipherNamingTheSuite()
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, Tls13Ciphers: "BOGUS"), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.SslCipher, result.Result.ExitCode);
        Assert.AreEqual("failed setting TLS 1.3 cipher suite: BOGUS", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    [DataRow("ECDHE-RSA-AES128-GCM-SHA256", null, "failed setting cipher list: ECDHE-RSA-AES128-GCM-SHA256")]
    [DataRow(null, "TLS_AES_128_GCM_SHA256", "failed setting TLS 1.3 cipher suite: TLS_AES_128_GCM_SHA256")]
    public async Task AuthenticateAsClientAsync_WithValidCiphersInTheOpenSslBuildOnWindows_FailsWithSslCipherInsteadOfThrowing(
        string? ciphers,
        string? tls13Ciphers,
        string expectedMessage)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Only Windows lacks CipherSuitesPolicy; elsewhere the OpenSSL build applies the list.");
        }

        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, Ciphers: ciphers, Tls13Ciphers: tls13Ciphers),
            CertificateHost,
            SslProtocols.None,
            OpenSslBuild);

        Assert.AreEqual(CurlExitCode.SslCipher, result.Result.ExitCode);
        Assert.AreEqual(expectedMessage, result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithValidCiphersOnThisPlatform_NeverLetsPlatformNotSupportedExceptionEscape()
    {
        var provider = new SslStreamTlsProvider(
            new TlsClientOptions(Insecure: true, Ciphers: "ECDHE-RSA-AES128-GCM-SHA256", Tls13Ciphers: "TLS_AES_128_GCM_SHA256"));

        var result = await HandshakeAsync(provider, CertificateHost, SslProtocols.Tls12);

        Assert.AreEqual(OperatingSystem.IsWindows() ? CurlExitCode.SslCipher : CurlExitCode.Ok, result.Result.ExitCode);
        if (result.Result.Connection is { } connection)
        {
            await connection.DisposeAsync();
        }
    }

    [TestMethod]
    [DataRow("ECDHE-RSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256)]
    [DataRow("TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384)]
    [DataRow("BOGUS:ECDHE-RSA-AES256-SHA", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA)]
    public async Task AuthenticateAsClientAsync_WithCiphersInTheOpenSslBuildOverTls12_NegotiatesTheNamedSuite(
        string ciphers,
        TlsCipherSuite expected)
    {
        AssertCipherSuitesPolicyIsSupported();

        var negotiated = await NegotiateAsync(new TlsClientOptions(Insecure: true, Ciphers: ciphers), SslProtocols.Tls12);

        Assert.AreEqual(expected, negotiated);
    }

    [TestMethod]
    [DataRow("TLS_AES_128_GCM_SHA256", TlsCipherSuite.TLS_AES_128_GCM_SHA256)]
    [DataRow("BOGUS:TLS_CHACHA20_POLY1305_SHA256", TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256)]
    public async Task AuthenticateAsClientAsync_WithTls13CiphersInTheOpenSslBuildOverTls13_NegotiatesTheNamedSuite(
        string tls13Ciphers,
        TlsCipherSuite expected)
    {
        AssertCipherSuitesPolicyIsSupported();
        await AssertTls13IsAvailableAsync();

        var negotiated = await NegotiateAsync(new TlsClientOptions(Insecure: true, Tls13Ciphers: tls13Ciphers), SslProtocols.Tls13);

        Assert.AreEqual(expected, negotiated);
    }

    private static void AssertCipherSuitesPolicyIsSupported()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("CipherSuitesPolicy is not supported on Windows, so no named suite can be negotiated here.");
        }
    }

    // The suite the server side agreed, after the OpenSSL build's handshake.
    private static async Task<TlsCipherSuite> NegotiateAsync(TlsClientOptions options, SslProtocols serverProtocols)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var serverStream = new SslStream(server);
        var serverTask = serverStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = s_serverCertificate,
            EnabledSslProtocols = serverProtocols,
        });
        var provider = new SslStreamTlsProvider(options, OpenSslBuild);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await serverTask;
        await result.Connection!.DisposeAsync();
        return serverStream.NegotiatedCipherSuite;
    }
}
