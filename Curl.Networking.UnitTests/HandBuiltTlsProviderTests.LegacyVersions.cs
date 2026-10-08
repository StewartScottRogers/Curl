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
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("minimum version", options.MinimumVersion);
        Diagnostics.Arrange("maximum version", options.MaximumVersion);
        Diagnostics.Arrange("server TLS protocol", serverVersion);
        ArrangeCertificate("server certificate", s_serverCertificate);
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

        Diagnostics.Bytes("request", request);
        Diagnostics.Bytes("canned response", response);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        await connection.WriteAsync(request, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        var answer = new byte[response.Length];
        await ReadExactlyAsync(connection, answer);

        var serverReceived = await serverTask;
        Diagnostics.Bytes("answer", answer);
        Diagnostics.Diff("request the server received", request, serverReceived);
        Diagnostics.Diff("response the client read", response, answer);
        Diagnostics.Assert("distinct record versions after the ClientHello", (ushort)serverVersion, string.Join(",", testServer.RecordVersions.Skip(1).Distinct()));
        CollectionAssert.AreEqual(request, serverReceived);
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
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("minimum version", minimum);
        Diagnostics.Arrange("maximum version", ceiling);
        Diagnostics.Arrange("server", "records every byte the client sends");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, false, Http11, CancellationToken.None);
        }

        ActConnectResult(result);
        var serverReceived = await serverTask;
        Diagnostics.Bytes("bytes the server received", serverReceived);
        Diagnostics.Act("TLS events", events.TlsEvents.Count);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("error message", "TLS connect error: error:0A0000BF:SSL routines::no protocols available", result.ErrorMessage);
        Assert.AreEqual("TLS connect error: error:0A0000BF:SSL routines::no protocols available", result.ErrorMessage);
        Diagnostics.Diff("protocol_version alert", new byte[] { 0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x46 }, serverReceived);
        CollectionAssert.AreEqual(new byte[] { 0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x46 }, serverReceived);
        Diagnostics.Assert("trust event type", nameof(TlsTrustEvent), events.TlsEvents.Single().GetType().Name);
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
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, AutoClientCertificate: true, MaximumVersion: Tls10");
        ArrangeCertificate("personal store certificate", s_clientCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await provider.AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        var serverReceived = await serverTask;
        Diagnostics.Bytes("bytes the server received", serverReceived);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("error message", TlsFailureMessages.OpenSslNoProtocolsAvailable, result.ErrorMessage);
        Assert.AreEqual(TlsFailureMessages.OpenSslNoProtocolsAvailable, result.ErrorMessage);
        Diagnostics.Assert("bytes the server received", 7, serverReceived.Length);
        Assert.HasCount(7, serverReceived);
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
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("minimum version", options.MinimumVersion);
        Diagnostics.Arrange("maximum version", options.MaximumVersion);
        Diagnostics.Arrange("server TLS protocol", serverVersion);
        ArrangeCertificate("server certificate", s_serverCertificate);
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

        Diagnostics.Bytes("request", request);
        Diagnostics.Bytes("canned response", response);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        await connection.WriteAsync(request, CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        var answer = new byte[response.Length];
        await ReadExactlyAsync(connection, answer);

        var serverReceived = await serverTask;
        Diagnostics.Bytes("answer", answer);
        Diagnostics.Diff("request the server received", request, serverReceived);
        Diagnostics.Diff("response the client read", response, answer);
        CollectionAssert.AreEqual(request, serverReceived);
        CollectionAssert.AreEqual(response, answer);
    }

    // ADR-0360 (BL-1143): the same options against a modern server still negotiate TLS 1.2 or 1.3.
    [TestMethod]
    [DataRow(SchannelBuild, TlsVersion.Tls10)]
    [DataRow(OpenSslBuild, TlsVersion.Tls10)]
    [DataRow(SchannelBuild, TlsVersion.Tls11)]
    [DataRow(OpenSslBuild, TlsVersion.Tls11)]
    public async Task AuthenticateAsClientAsync_WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion(
        bool matchesSchannelBuild,
        TlsVersion legacyVersion)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("minimum version", legacyVersion);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);

        using (Diagnostics.Phase("TLS handshake"))
        {
            await AssertLegacyMinimumNegotiatesTheServersVersionAsync(matchesSchannelBuild, legacyVersion, SslProtocols.Tls12);
        }

        Diagnostics.Act("handshake", "completed");
        Diagnostics.Assert("negotiated version, exit code and echo checked by the helper", SslProtocols.Tls12, SslProtocols.Tls12);
    }

    // The SslStream test server cannot speak TLS 1.3 on macOS (BL-1176).
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow(SchannelBuild, TlsVersion.Tls10)]
    [DataRow(OpenSslBuild, TlsVersion.Tls10)]
    [DataRow(SchannelBuild, TlsVersion.Tls11)]
    [DataRow(OpenSslBuild, TlsVersion.Tls11)]
    public async Task AuthenticateAsClientAsync_WithALegacyMinimumAgainstATls13Server_NegotiatesTls13(
        bool matchesSchannelBuild,
        TlsVersion legacyVersion)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("minimum version", legacyVersion);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls13);

        using (Diagnostics.Phase("TLS handshake"))
        {
            await AssertLegacyMinimumNegotiatesTheServersVersionAsync(matchesSchannelBuild, legacyVersion, SslProtocols.Tls13);
        }

        Diagnostics.Act("handshake", "completed");
        Diagnostics.Assert("negotiated version, exit code and echo checked by the helper", SslProtocols.Tls13, SslProtocols.Tls13);
    }

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
