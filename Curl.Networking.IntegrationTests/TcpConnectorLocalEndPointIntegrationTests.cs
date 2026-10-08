using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

[TestClass]
public sealed class TcpConnectorLocalEndPointIntegrationTests
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
        var connector = new TcpConnector(
            new SystemDnsResolver(),
            new TcpDialer(),
            new SslStreamTlsProvider(new TlsClientOptions(Insecure: true)),
            TimeProvider.System);

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
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var connector = new TcpConnector(
            new SystemDnsResolver(),
            new TcpDialer(),
            new SslStreamTlsProvider(new TlsClientOptions(Insecure: true)),
            TimeProvider.System);
        var server = AcceptTlsAsync(listener, serverCertificate, cancellation.Token);

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", port, UseTls: true), cancellation.Token);

        await using var connection = result.Connection!;
        var (serverSide, serverStream) = await server;
        using var serverConnection = serverSide;
        await using var serverTls = serverStream;

        Assert.IsTrue(connection.IsSecure);
        Assert.IsNotNull(connection.LocalEndPoint);
        Assert.AreEqual(result.LocalEndPoint, connection.LocalEndPoint);
        Assert.AreEqual(serverSide.Client.RemoteEndPoint, connection.LocalEndPoint);
    }

    private static async Task<(TcpClient Connection, SslStream Stream)> AcceptTlsAsync(
        TcpListener listener, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        var serverSide = await listener.AcceptTcpClientAsync(cancellationToken);
        var sslStream = new SslStream(serverSide.GetStream(), leaveInnerStreamOpen: true);
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

        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }
}
