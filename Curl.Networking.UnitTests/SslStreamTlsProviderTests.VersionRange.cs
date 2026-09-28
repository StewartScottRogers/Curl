using System.Net.Security;
using System.Security.Authentication;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The TLS version range <see cref="SslStreamTlsProvider" /> offers, from
/// <see cref="TlsClientOptions.MinimumVersion" /> up to <see cref="TlsClientOptions.MaximumVersion" />,
/// and how each build reports a range a TLS 1.2-only server cannot meet, as curl 8.21.0
/// (Schannel) and curl 8.18.0 (OpenSSL) report it (measured 2026-09-28, BL-502).
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.SystemDefault, SslProtocols.None)]
    [DataRow(TlsVersion.Tls10, TlsVersion.Tls11, TlsVersionRange.Tls10 | TlsVersionRange.Tls11)]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.Tls12, TlsVersionRange.Tls10 | TlsVersionRange.Tls11 | SslProtocols.Tls12)]
    [DataRow(TlsVersion.Tls12, TlsVersion.SystemDefault, SslProtocols.Tls12 | SslProtocols.Tls13)]
    [DataRow(TlsVersion.Tls13, TlsVersion.Tls13, SslProtocols.Tls13)]
    public async Task AuthenticateAsClientAsync_WithAVersionRange_OffersEveryVersionInIt(
        TlsVersion minimum, TlsVersion maximum, SslProtocols expected)
    {
        SslClientAuthenticationOptions? handshakeOptions = null;
        var provider = new SslStreamTlsProvider(
            new TlsClientOptions(Insecure: true, MinimumVersion: minimum, MaximumVersion: maximum), SchannelBuild)
        {
            AuthenticateSslStreamAsClientAsync = (_, authenticationOptions, _) =>
            {
                handshakeOptions = authenticationOptions;
                throw new AuthenticationException("The test stops before the handshake.");
            },
        };
        var (client, _) = InMemoryDuplexStream.CreatePair();

        await provider.AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(expected, handshakeOptions!.EnabledSslProtocols);
    }

    [TestMethod]
    public void Constructor_MinimumAboveTheCeiling_ThrowsArgumentException()
    {
        var options = new TlsClientOptions(MinimumVersion: TlsVersion.Tls13, MaximumVersion: TlsVersion.Tls12);

        Assert.ThrowsExactly<ArgumentException>(() => new SslStreamTlsProvider(options, SchannelBuild));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.Tls11)]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.Tls10)]
    [DataRow(TlsVersion.Tls11, TlsVersion.Tls11)]
    [DataRow(TlsVersion.Tls10, TlsVersion.Tls10)]
    [DataRow(TlsVersion.Tls10, TlsVersion.Tls11)]
    public async Task AuthenticateAsClientAsync_WithACeilingBelowTls12AgainstATls12OnlyServerInTheSchannelBuildOnWindows_ReportsTheMeasuredLine(
        TlsVersion minimum, TlsVersion maximum)
    {
        // curl 8.21.0 (Schannel) --tls-max 1.1, --tls-max 1.0, --tlsv1.1 --tls-max 1.1, --tlsv1.0 --tls-max 1.0 and
        // --tlsv1.0 --tls-max 1.1 against a TLS 1.2 server: each exit 35 with this line (BL-502).
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, MinimumVersion: minimum, MaximumVersion: maximum),
            CertificateHost,
            SslProtocols.Tls12,
            SchannelBuild);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual("schannel: failed to receive handshake, SSL/TLS connection failed", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.Tls11)]
    [DataRow(TlsVersion.SystemDefault, TlsVersion.Tls10)]
    [DataRow(TlsVersion.Tls11, TlsVersion.Tls11)]
    [DataRow(TlsVersion.Tls10, TlsVersion.Tls10)]
    public async Task AuthenticateAsClientAsync_WithACeilingBelowTls12InTheOpenSslBuildOnLinux_ReportsTheMeasuredLine(
        TlsVersion minimum, TlsVersion maximum)
    {
        // curl 8.18.0 (OpenSSL 3.5.5, Ubuntu) --tls-max 1.1, --tls-max 1.0, --tlsv1.1 --tls-max 1.1 and
        // --tlsv1.0 --tls-max 1.0: each exit 35 with this line, OpenSSL refusing before any byte is sent
        // (BL-502). Linux only: macOS's SslStream does not run on OpenSSL.
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, MinimumVersion: minimum, MaximumVersion: maximum),
            CertificateHost,
            SslProtocols.Tls12,
            OpenSslBuild);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual("TLS connect error: error:0A0000BF:SSL routines::no protocols available", result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithATls12CeilingAgainstATls12OnlyServer_Succeeds()
    {
        // curl 8.21.0 (Schannel) -k --tls-max 1.2 against a TLS 1.2 server: exit 0 (BL-502).
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, MaximumVersion: TlsVersion.Tls12),
            CertificateHost,
            SslProtocols.Tls12);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        await result.Result.Connection!.DisposeAsync();
    }
}
