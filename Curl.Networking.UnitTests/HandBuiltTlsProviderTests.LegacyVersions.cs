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
    // The OpenSSL build refuses a legacy ceiling instead (BL-1152, ADR-0364; below).
    [TestMethod]
    [DataRow(SchannelBuild, TlsVersion.Tls10, TlsProtocolVersion.Tls10)]
    [DataRow(SchannelBuild, TlsVersion.Tls11, TlsProtocolVersion.Tls11)]
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

    // Measured 2026-10-02 with Ubuntu's curl 8.18.0 (OpenSSL 3.5.5), --tls-max 1.0 and 1.1, with
    // and without -k and --tlsv1.0 (BL-1152, ADR-0364): no ClientHello, a protocol_version alert in
    // a 3.3 record, then exit 35 with OpenSSL's "no protocols available", after the trust lines.
    [TestMethod]
    [DataRow(TlsVersion.Tls10, TlsVersion.SystemDefault)]
    [DataRow(TlsVersion.Tls11, TlsVersion.SystemDefault)]
    [DataRow(TlsVersion.Tls10, TlsVersion.Tls10)]
    public async Task AuthenticateAsClientAsync_WithALegacyCeilingInTheOpenSslBuild_SendsAProtocolVersionAlertAndFailsWithNoProtocolsAvailable(
        TlsVersion ceiling,
        TlsVersion minimum)
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: minimum, MaximumVersion: ceiling);
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            using var received = new MemoryStream();
            await server.CopyToAsync(received);
            return received.ToArray();
        });
        var events = new RecordingTransferEvents();

        var result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, false, Http11, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("TLS connect error: error:0A0000BF:SSL routines::no protocols available", result.ErrorMessage);
        CollectionAssert.AreEqual(new byte[] { 0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x46 }, await serverTask);
        Assert.IsInstanceOfType<TlsTrustEvent>(events.TlsEvents.Single());
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithALegacyCeilingAndAClientCertificateInTheOpenSslBuild_FailsWithNoProtocolsAvailable()
    {
        var store = new FakeClientCertificateStore
        {
            Certificates = [X509CertificateLoader.LoadPkcs12(s_clientCertificate.Export(X509ContentType.Pkcs12), null)],
        };
        var provider = new HandBuiltTlsProvider(
            new TlsClientOptions(Insecure: true, AutoClientCertificate: true, MaximumVersion: TlsVersion.Tls10), OpenSslBuild, TimeProvider.System, store, SystemTlsRandomSource.Instance);
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            using var received = new MemoryStream();
            await server.CopyToAsync(received);
            return received.ToArray();
        });

        var result = await provider.AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(TlsFailureMessages.OpenSslNoProtocolsAvailable, result.ErrorMessage);
        Assert.HasCount(7, await serverTask);
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
