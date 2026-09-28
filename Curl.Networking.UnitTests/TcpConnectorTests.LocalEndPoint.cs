using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that the connection <see cref="TcpConnector" /> returns over the production
/// <see cref="TcpDialer" /> reports its socket's local end point, which FTP's <c>-P -</c>
/// announces (ADR-0102, BL-456), and that the TLS connection
/// <see cref="SslStreamTlsProvider" /> wraps it in still reports it, so <c>-P -</c> works
/// over <c>ftps://</c> (BL-465). They dial a loopback <see cref="TcpConnectionListener" />,
/// so they are <c>Integration</c> tests, as <see cref="TcpDialerTests" /> is.
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public async Task ConnectAsync_OverTheTcpDialer_ReturnsAConnectionThatReportsItsLocalEndPoint()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var listened = await new TcpConnectionListener().ListenAsync(
            new ListenTarget(IPAddress.Loopback, 0, 0), cancellation.Token);
        await using var pending = listened.PendingConnection!;
        var port = ((IPEndPoint)pending.LocalEndPoint).Port;
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new TcpDialer(), new FakeTlsProvider(), TimeProvider.System);

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", port, UseTls: false), cancellation.Token);
        await using var connection = result.Connection!;
        var accepted = await pending.AcceptAsync(cancellation.Token);
        await using var serverSide = accepted.Connection!;

        Assert.IsNotNull(connection.LocalEndPoint);
        Assert.AreEqual(result.LocalEndPoint, connection.LocalEndPoint);
        Assert.AreEqual(serverSide.RemoteEndPoint, connection.LocalEndPoint);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ConnectAsync_WithTlsToALoopbackTlsServer_ReturnsAConnectionThatReportsItsSocketsLocalEndPoint()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var serverCertificate = CreateLoopbackServerCertificate();
        var listened = await new TcpConnectionListener().ListenAsync(
            new ListenTarget(IPAddress.Loopback, 0, 0), cancellation.Token);
        await using var pending = listened.PendingConnection!;
        var port = ((IPEndPoint)pending.LocalEndPoint).Port;
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), new TcpDialer(), new SslStreamTlsProvider(new TlsClientOptions(Insecure: true)), TimeProvider.System);
        var server = AcceptTlsAsync(pending, serverCertificate, cancellation.Token);

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", port, UseTls: true), cancellation.Token);
        await using var connection = result.Connection!;
        var (serverSide, serverStream) = await server;
        await using var serverConnection = serverSide;
        await using var serverTls = serverStream;

        Assert.IsTrue(connection.IsSecure);
        Assert.IsNotNull(connection.LocalEndPoint);
        Assert.AreEqual(result.LocalEndPoint, connection.LocalEndPoint);
        Assert.AreEqual(serverSide.RemoteEndPoint, connection.LocalEndPoint);
    }

    private static async Task<(IConnection Connection, SslStream Stream)> AcceptTlsAsync(
        IPendingConnection pending, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        var accepted = await pending.AcceptAsync(cancellationToken);
        var serverSide = accepted.Connection!;
        var sslStream = new SslStream(new ConnectionStream(serverSide), leaveInnerStreamOpen: true);
        await sslStream.AuthenticateAsServerAsync(
            new SslServerAuthenticationOptions { ServerCertificate = certificate }, cancellationToken);
        return (serverSide, sslStream);
    }

    private static X509Certificate2 CreateLoopbackServerCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        // A server certificate with an ephemeral key can fail the handshake on Windows;
        // reloading it from PKCS#12 gives it a key the platform can use.
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }
}
