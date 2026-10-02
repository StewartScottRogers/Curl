using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// TLS 1.0 and 1.1 through the hand-built client (ADR-0140's legacy-versions row, BL-714):
/// a <c>--tls-max 1.0</c> or <c>1.1</c> range completes a transfer against a server that
/// speaks only that version, on every platform and as either build, since no operating-system
/// TLS stack takes part (<see cref="LegacyTlsTestServer" />).
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    [TestMethod]
    [DataRow(SchannelBuild, TlsVersion.Tls10, TlsProtocolVersion.Tls10)]
    [DataRow(OpenSslBuild, TlsVersion.Tls10, TlsProtocolVersion.Tls10)]
    [DataRow(SchannelBuild, TlsVersion.Tls11, TlsProtocolVersion.Tls11)]
    [DataRow(OpenSslBuild, TlsVersion.Tls11, TlsProtocolVersion.Tls11)]
    public async Task AuthenticateAsClientAsync_WithTheRangeCappedAtALegacyVersion_CompletesATransferWithAServerSpeakingOnlyIt(
        bool matchesSchannelBuild,
        TlsVersion legacyVersion,
        TlsProtocolVersion serverVersion)
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: legacyVersion, MaximumVersion: legacyVersion);
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(options));
        var request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");
        var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");
        using var rsaKey = s_serverCertificate.GetRSAPrivateKey()!;
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var testServer = new LegacyTlsTestServer(server, serverVersion, rsaKey, s_serverCertificate.RawData);
        var serverTask = Task.Run(async () =>
        {
            await testServer.HandshakeAsync();
            var received = await testServer.ReceiveApplicationDataAsync(request.Length);
            await testServer.SendAsync(Curl.Tls.TlsContentType.ApplicationData, response);
            return received;
        });

        var result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        await connection.WriteAsync(request, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        var answer = new byte[response.Length];
        await ReadExactlyAsync(connection, answer);

        CollectionAssert.AreEqual(request, await serverTask);
        CollectionAssert.AreEqual(response, answer);
        Assert.IsTrue(testServer.RecordVersions.Skip(1).All(recordVersion => recordVersion == (ushort)serverVersion), "every record after the ClientHello carries the negotiated version");
    }

    // ADR-0360 (BL-1143): --tlsv1.0 or --tlsv1.1 alone reaches a server speaking only that version.
    [TestMethod]
    [DataRow(SchannelBuild, TlsVersion.Tls10, TlsProtocolVersion.Tls10)]
    [DataRow(OpenSslBuild, TlsVersion.Tls10, TlsProtocolVersion.Tls10)]
    [DataRow(SchannelBuild, TlsVersion.Tls11, TlsProtocolVersion.Tls11)]
    [DataRow(OpenSslBuild, TlsVersion.Tls11, TlsProtocolVersion.Tls11)]
    public async Task AuthenticateAsClientAsync_WithALegacyMinimumAndNoCeiling_CompletesATransferWithAServerSpeakingOnlyIt(
        bool matchesSchannelBuild,
        TlsVersion legacyVersion,
        TlsProtocolVersion serverVersion)
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: legacyVersion);
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(options));
        var request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");
        var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok");
        using var rsaKey = s_serverCertificate.GetRSAPrivateKey()!;
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var testServer = new LegacyTlsTestServer(server, serverVersion, rsaKey, s_serverCertificate.RawData);
        var serverTask = Task.Run(async () =>
        {
            await testServer.HandshakeAsync();
            var received = await testServer.ReceiveApplicationDataAsync(request.Length);
            await testServer.SendAsync(Curl.Tls.TlsContentType.ApplicationData, response);
            return received;
        });

        var result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        await connection.WriteAsync(request, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        var answer = new byte[response.Length];
        await ReadExactlyAsync(connection, answer);

        CollectionAssert.AreEqual(request, await serverTask);
        CollectionAssert.AreEqual(response, answer);
    }

    // ADR-0360 (BL-1143): the same options against a modern server still negotiate TLS 1.2 or 1.3.
    [TestMethod]
    [DataRow(SchannelBuild, TlsVersion.Tls10)]
    [DataRow(OpenSslBuild, TlsVersion.Tls10)]
    [DataRow(SchannelBuild, TlsVersion.Tls11)]
    [DataRow(OpenSslBuild, TlsVersion.Tls11)]
    public Task AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion(
        bool matchesSchannelBuild,
        TlsVersion legacyVersion) =>
        AssertLegacyMinimumNegotiatesTheServersVersionAsync(matchesSchannelBuild, legacyVersion, SslProtocols.Tls12);

    // The SslStream test server cannot speak TLS 1.3 on macOS (BL-1176).
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow(SchannelBuild, TlsVersion.Tls10)]
    [DataRow(OpenSslBuild, TlsVersion.Tls10)]
    [DataRow(SchannelBuild, TlsVersion.Tls11)]
    [DataRow(OpenSslBuild, TlsVersion.Tls11)]
    public Task AuthenticateAsClientAsync_WithALegacyMinimumAgainstATls13Server_NegotiatesTls13(
        bool matchesSchannelBuild,
        TlsVersion legacyVersion) =>
        AssertLegacyMinimumNegotiatesTheServersVersionAsync(matchesSchannelBuild, legacyVersion, SslProtocols.Tls13);

    private static async Task AssertLegacyMinimumNegotiatesTheServersVersionAsync(
        bool matchesSchannelBuild,
        TlsVersion legacyVersion,
        SslProtocols serverVersion)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, serverVersion);
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: legacyVersion);
        Assert.AreEqual(TlsClientRoute.HandBuilt, TlsClientRouting.Choose(options));

        var result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        Assert.AreEqual("ping", Encoding.ASCII.GetString(await EchoAsync(connection, "ping")));
        Assert.AreEqual(serverVersion, Assert.ContainsSingle(events.Handshakes).ProtocolVersion);
        await connection.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    private static async Task ReadExactlyAsync(IConnection connection, Memory<byte> buffer)
    {
        for (var offset = 0; offset < buffer.Length;)
        {
            var read = await connection.ReadAsync(buffer[offset..], CancellationToken.None);
            Assert.AreNotEqual(0, read, "the connection ended before the whole response arrived");
            offset += read;
        }
    }
}
