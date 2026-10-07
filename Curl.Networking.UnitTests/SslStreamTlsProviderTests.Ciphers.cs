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

        Diagnostics.Assert("exit code", CurlExitCode.SslCipher, result.Result.ExitCode);
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

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithUnknownCiphersInTheOpenSslBuild_FailsWithSslCipherNamingTheList()
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, Ciphers: "BOGUS"), CertificateHost, SslProtocols.None, OpenSslBuild);

        Diagnostics.Assert("exit code", CurlExitCode.SslCipher, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslCipher, result.Result.ExitCode);
        Assert.AreEqual("failed setting cipher list: BOGUS", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithUnknownTls13CiphersInTheOpenSslBuild_FailsWithSslCipherNamingTheSuite()
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, Tls13Ciphers: "BOGUS"), CertificateHost, SslProtocols.None, OpenSslBuild);

        Diagnostics.Assert("exit code", CurlExitCode.SslCipher, result.Result.ExitCode);
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
        Diagnostics.Arrange("platform", "Windows only");
        Diagnostics.Act("running on Windows", OperatingSystem.IsWindows());
        Diagnostics.Assert("running on Windows", true, OperatingSystem.IsWindows());
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Only Windows lacks CipherSuitesPolicy; elsewhere the OpenSSL build applies the list.");
        }

        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, Ciphers: ciphers, Tls13Ciphers: tls13Ciphers),
            CertificateHost,
            SslProtocols.None,
            OpenSslBuild);

        Diagnostics.Assert("exit code", CurlExitCode.SslCipher, result.Result.ExitCode);
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

        Diagnostics.Assert("exit code", OperatingSystem.IsWindows() ? CurlExitCode.SslCipher : CurlExitCode.Ok, result.Result.ExitCode);
        Assert.AreEqual(OperatingSystem.IsWindows() ? CurlExitCode.SslCipher : CurlExitCode.Ok, result.Result.ExitCode);
        if (result.Result.Connection is { } connection)
        {
            await connection.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithValidCiphersInTheOpenSslBuild_SetsThePolicyBuiltFromTheSelectedSuites()
    {
        var options = new TlsClientOptions(
            Insecure: true, Ciphers: "BOGUS:ECDHE-RSA-AES128-GCM-SHA256", Tls13Ciphers: "TLS_AES_128_GCM_SHA256");
        var factory = new RecordingCipherSuitesPolicyFactory();
        SslClientAuthenticationOptions? handshakeOptions = null;
        var provider = new SslStreamTlsProvider(options, OpenSslBuild)
        {
            CipherSuitesPolicyFactory = factory,
            AuthenticateSslStreamAsClientAsync = (_, authenticationOptions, _) =>
            {
                handshakeOptions = authenticationOptions;
                throw new AuthenticationException("The test stops before the handshake.");
            },
        };
        var (client, _) = InMemoryDuplexStream.CreatePair();
        ArrangeOptions(options);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        ActResult(result);
        Diagnostics.Act("policy suites", string.Join(",", factory.Suites ?? []));
        var (expectedSuites, _) = OpenSslCipherSuites.Select(options.Ciphers, options.Tls13Ciphers);
        Diagnostics.Assert("policy suites", string.Join(",", expectedSuites ?? []), string.Join(",", factory.Suites ?? []));
        CollectionAssert.AreEqual(expectedSuites!.ToArray(), factory.Suites);
        Assert.AreSame<object>(factory.Policy, handshakeOptions!.CipherSuitesPolicy);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
    }

    [TestMethod]
    [DataRow("ECDHE-RSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256)]
    [DataRow("TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384)]
    [DataRow("BOGUS:ECDHE-RSA-AES256-SHA", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA)]
    public async Task AuthenticateAsClientAsync_WithCiphersInTheOpenSslBuildOverTls12_NegotiatesTheNamedSuite(
        string ciphers,
        TlsCipherSuite expected)
    {
        Diagnostics.Arrange("ciphers", ciphers);
        AssertCipherSuitesPolicyIsSupported();

        var negotiated = await NegotiateAsync(
            new TlsClientOptions(Insecure: true, Ciphers: ciphers), SslProtocols.Tls12, s_tls12ServerSuites);

        Diagnostics.Assert("negotiated suite", expected, negotiated);
        Assert.AreEqual(expected, negotiated);
    }

    [TestMethod]
    [DataRow("TLS_AES_128_GCM_SHA256", TlsCipherSuite.TLS_AES_128_GCM_SHA256)]
    [DataRow("BOGUS:TLS_CHACHA20_POLY1305_SHA256", TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256)]
    public async Task AuthenticateAsClientAsync_WithTls13CiphersInTheOpenSslBuildOverTls13_NegotiatesTheNamedSuite(
        string tls13Ciphers,
        TlsCipherSuite expected)
    {
        Diagnostics.Arrange("TLS 1.3 ciphers", tls13Ciphers);
        AssertCipherSuitesPolicyIsSupported();
        await AssertTls13IsAvailableAsync();

        var negotiated = await NegotiateAsync(
            new TlsClientOptions(Insecure: true, Tls13Ciphers: tls13Ciphers), SslProtocols.Tls13, serverSuites: null);

        Diagnostics.Assert("negotiated suite", expected, negotiated);
        Assert.AreEqual(expected, negotiated);
    }

    private void AssertCipherSuitesPolicyIsSupported()
    {
        Diagnostics.Act("CipherSuitesPolicy supported", !OperatingSystem.IsWindows());
        Diagnostics.Assert("CipherSuitesPolicy supported", true, !OperatingSystem.IsWindows());
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("CipherSuitesPolicy is not supported on Windows, so no named suite can be negotiated here.");
        }
    }

    // Every TLS 1.2 suite the data rows expect, offered together so the client's list alone
    // decides which is negotiated. .NET's default server list on Linux holds only AEAD suites,
    // so without this the CBC row's ECDHE-RSA-AES256-SHA is never on offer there.
    private static readonly TlsCipherSuite[] s_tls12ServerSuites =
    [
        TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
        TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
        TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA,
    ];

    // The suite the server side agreed, after the OpenSSL build's handshake. The server offers
    // serverSuites when given, and its platform default otherwise.
    private async Task<TlsCipherSuite> NegotiateAsync(
        TlsClientOptions options, SslProtocols serverProtocols, TlsCipherSuite[]? serverSuites)
    {
        ArrangeOptions(options);
        Diagnostics.Arrange("server", $"{serverProtocols}, suites {(serverSuites is null ? "default" : string.Join(",", serverSuites))}");
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await using var serverStream = new SslStream(server);
        var serverTask = serverStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = s_serverCertificate,
            EnabledSslProtocols = serverProtocols,
            CipherSuitesPolicy = serverSuites is null || OperatingSystem.IsWindows() ? null : new CipherSuitesPolicy(serverSuites),
        });
        var provider = new SslStreamTlsProvider(options, OpenSslBuild);

        ConnectResult result;
        using (Diagnostics.Phase("handshake"))
        {
            result = await provider.AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        }

        ActResult(result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await serverTask;
        Diagnostics.Act("negotiated suite", serverStream.NegotiatedCipherSuite);
        await result.Connection!.DisposeAsync();
        return serverStream.NegotiatedCipherSuite;
    }
}
