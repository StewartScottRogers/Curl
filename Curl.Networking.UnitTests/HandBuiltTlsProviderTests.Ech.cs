using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Networking.Fakes.Tls13Server;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// <c>--ech</c> through the hand-built client (ADR-0327, BL-711): the ClientHello each mode
/// sends, where its configuration comes from, and how <c>hard</c> and a rejected offer fail.
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    private const byte EchTestConfigId = 0x2a;

    private const string EchPublicName = "public.test";

    private const string EchHost = "curl.test";

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEchGrease_SendsAGreaseExtensionLastUnderTheTargetHost()
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", "grease");
        Diagnostics.Arrange("EchConfigList", EchConfigListBase64());
        Diagnostics.Arrange("target host", EchHost);

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(Ech: "grease", EchConfigList: EchConfigListBase64()), OpenSslBuild, EchHost, Http11);
        }

        var hello = DecodeClientHello(record);
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("last extension", ExtensionTypes(hello)[^1]);
        Diagnostics.Act("server_name", ServerNameOf(hello));
        Diagnostics.Assert("last extension", TlsExtensionType.EncryptedClientHello, ExtensionTypes(hello)[^1]);
        Diagnostics.Assert("server_name", EchHost, ServerNameOf(hello));
        Assert.AreEqual(TlsExtensionType.EncryptedClientHello, ExtensionTypes(hello)[^1]);
        Assert.AreEqual(EchHost, ServerNameOf(hello));
        Assert.AreEqual(0, ExtensionData(hello, TlsExtensionType.EncryptedClientHello)[0]);
    }

    [TestMethod]
    [DataRow("false")]
    [DataRow(null)]
    public async Task AuthenticateAsClientAsync_WithEchOff_SendsNoEchExtension(string? mode)
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", mode ?? "(none)");
        Diagnostics.Arrange("target host", EchHost);

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(Ech: mode), OpenSslBuild, EchHost, Http11);
        }

        var hello = DecodeClientHello(record);
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("extension types", string.Join(",", ExtensionTypes(hello)));
        Diagnostics.Assert("ECH extension offered", false, ExtensionTypes(hello).Contains(TlsExtensionType.EncryptedClientHello));
        Diagnostics.Assert("server_name", EchHost, ServerNameOf(hello));
        Assert.IsFalse(ExtensionTypes(hello).Contains(TlsExtensionType.EncryptedClientHello));
        Assert.AreEqual(EchHost, ServerNameOf(hello));
    }

    [TestMethod]
    [DataRow("false")]
    [DataRow(null)]
    public async Task AuthenticateAsClientAsync_WithOnlyAPublicName_FailsAsHardWithExit35(string? mode)
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", mode ?? "(none)");
        Diagnostics.Arrange("EchPublicName", "pn.test");
        var (plaintext, stream) = Unanswered();

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(new TlsClientOptions(Insecure: true, Ech: mode, EchPublicName: "pn.test"), OpenSslBuild)
                .AuthenticateAsClientAsync(plaintext, EchHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Act("connection disposed", stream.IsDisposed);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("error message", "SSL connect error", result.ErrorMessage);
        Diagnostics.Assert("connection disposed", true, stream.IsDisposed);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("SSL connect error", result.ErrorMessage);
        Assert.IsTrue(stream.IsDisposed);
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("hard")]
    [DataRow("false")]
    [DataRow(null)]
    public async Task AuthenticateAsClientAsync_WithAnEclList_SendsTheOuterHelloForItsConfig(string? mode)
    {
        var lookup = new FakeEchConfigListLookup(EchConfigListBytes("dns.test"));
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("Ech", mode ?? "(none)");
        Diagnostics.Arrange("EchConfigList", EchConfigListBase64());
        Diagnostics.Arrange("DNS lookup", "fake lookup answering a config list for dns.test");

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(Ech: mode, EchConfigList: EchConfigListBase64()), SchannelBuild, EchHost, Http11, lookup);
        }

        var hello = DecodeClientHello(record);
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("server_name", ServerNameOf(hello));
        Diagnostics.Act("DNS lookups asked", lookup.Asked.ToArray().Length);
        Diagnostics.Assert("server_name", EchPublicName, ServerNameOf(hello));
        Diagnostics.Assert("DNS lookups asked", 0, lookup.Asked.ToArray().Length);
        AssertOffersTheTestConfig(hello, EchPublicName);
        Assert.IsEmpty(lookup.Asked);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAPublicName_NamesItInTheOuterHello()
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", "true");
        Diagnostics.Arrange("EchPublicName", "pn.test");
        Diagnostics.Arrange("EchConfigList", EchConfigListBase64());

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(Ech: "true", EchPublicName: "pn.test", EchConfigList: EchConfigListBase64()), OpenSslBuild, EchHost, Http11);
        }

        var hello = DecodeClientHello(record);
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("server_name", ServerNameOf(hello));
        Diagnostics.Assert("server_name", "pn.test", ServerNameOf(hello));
        AssertOffersTheTestConfig(hello, "pn.test");
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("hard")]
    public async Task AuthenticateAsClientAsync_WithoutAnEclList_OffersTheHostsListFromDns(string mode)
    {
        var lookup = new FakeEchConfigListLookup(EchConfigListBytes("dns.test"));

        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", mode);
        Diagnostics.Arrange("DNS lookup", "fake lookup answering a config list for dns.test");

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(Ech: mode), OpenSslBuild, EchHost, Http11, lookup);
        }

        var hello = DecodeClientHello(record);
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("server_name", ServerNameOf(hello));
        Diagnostics.Act("DNS lookups asked", lookup.Asked.ToArray().Length);
        Diagnostics.Assert("server_name", "dns.test", ServerNameOf(hello));
        Diagnostics.Assert("DNS lookups asked", 1, lookup.Asked.ToArray().Length);
        AssertOffersTheTestConfig(hello, "dns.test");
        CollectionAssert.AreEqual(new[] { (EchHost, 443) }, lookup.Asked.ToArray());
    }

    public static IEnumerable<object?[]> UnusableEchSources =>
    [
        [null, null],
        [null, new FakeEchConfigListLookup(null)],
        [null, new FakeEchConfigListLookup([])],
        [null, new FakeEchConfigListLookup(EchConfigListBytes(EchPublicName, kemId: 0x9999))],
        [null, new FakeEchConfigListLookup([0x00, 0x05, 0xfe])],
        ["not base64!", null],
    ];

    [TestMethod]
    [DynamicData(nameof(UnusableEchSources))]
    public async Task AuthenticateAsClientAsync_WithEchTrueAndNoUsableList_SendsAPlainHello(string? eclList, FakeEchConfigListLookup? lookup)
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", "true");
        Diagnostics.Arrange("EchConfigList", eclList ?? "(none)");
        Diagnostics.Arrange("DNS lookup", lookup is null ? "(none)" : "fake lookup");

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(Ech: "true", EchConfigList: eclList), OpenSslBuild, EchHost, Http11, lookup);
        }

        var hello = DecodeClientHello(record);
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("extension types", string.Join(",", ExtensionTypes(hello)));
        Diagnostics.Assert("ECH extension offered", false, ExtensionTypes(hello).Contains(TlsExtensionType.EncryptedClientHello));
        Diagnostics.Assert("server_name", EchHost, ServerNameOf(hello));
        Assert.IsFalse(ExtensionTypes(hello).Contains(TlsExtensionType.EncryptedClientHello));
        Assert.AreEqual(EchHost, ServerNameOf(hello));
    }

    [TestMethod]
    [DynamicData(nameof(UnusableEchSources))]
    public async Task AuthenticateAsClientAsync_WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte(string? eclList, FakeEchConfigListLookup? lookup)
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", "hard");
        Diagnostics.Arrange("EchConfigList", eclList ?? "(none)");
        Diagnostics.Arrange("DNS lookup", lookup is null ? "(none)" : "fake lookup");
        var (plaintext, stream) = Unanswered();

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(new TlsClientOptions(Insecure: true, Ech: "hard", EchConfigList: eclList), OpenSslBuild, lookup)
                .AuthenticateAsClientAsync(plaintext, EchHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Act("connection disposed", stream.IsDisposed);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("error message", "SSL connect error", result.ErrorMessage);
        Diagnostics.Assert("connection disposed", true, stream.IsDisposed);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("SSL connect error", result.ErrorMessage);
        Assert.IsTrue(stream.IsDisposed);
    }

    [TestMethod]
    [DataRow("hard", SchannelBuild)]
    [DataRow("true", OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithAUsableListBelowTls13_FailsWithOpenSslsNoProtocolsAvailable(string mode, bool matchesSchannelBuild)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("Ech", mode);
        Diagnostics.Arrange("EchConfigList", EchConfigListBase64());
        Diagnostics.Arrange("MaximumVersion", "TLS 1.2 ceiling");
        var (plaintext, stream) = Unanswered();

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(Tls12Only(new TlsClientOptions(Insecure: true, Ech: mode, EchConfigList: EchConfigListBase64())), matchesSchannelBuild)
                .AuthenticateAsClientAsync(plaintext, EchHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Act("connection disposed", stream.IsDisposed);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("error message", "TLS connect error: error:0A0000BF:SSL routines::no protocols available", result.ErrorMessage);
        Diagnostics.Assert("connection disposed", true, stream.IsDisposed);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("TLS connect error: error:0A0000BF:SSL routines::no protocols available", result.ErrorMessage);
        Assert.IsTrue(stream.IsDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEchGreaseBelowTls13_SendsAPlainTls12Hello()
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", "grease");
        Diagnostics.Arrange("EchConfigList", EchConfigListBase64());
        Diagnostics.Arrange("MaximumVersion", "TLS 1.2 ceiling");

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(Tls12Only(new TlsClientOptions(Ech: "grease", EchConfigList: EchConfigListBase64())), OpenSslBuild, EchHost, Http11);
        }

        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("extension types", string.Join(",", ExtensionTypes(DecodeClientHello(record))));
        Diagnostics.Assert("ECH extension offered", false, ExtensionTypes(DecodeClientHello(record)).Contains(TlsExtensionType.EncryptedClientHello));
        Assert.IsFalse(ExtensionTypes(DecodeClientHello(record)).Contains(TlsExtensionType.EncryptedClientHello));
    }

    // The ECH result rides on the completed handshake's event, for the -v line after
    // `SSL connection using` (BL-1170); a TLS 1.2 server keeps it off macOS' TLS 1.3 gap.
    [TestMethod]
    [DataRow("grease", "status is sent GREASE, inner is NULL, outer is NULL")]
    [DataRow("true", "status is not configured, inner is NULL, outer is NULL")]
    [DataRow("false", null)]
    [DataRow(null, null)]
    public async Task AuthenticateAsClientAsync_WithEchToACompletedHandshake_ReportsTheEchResult(string? mode, string? expected)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", mode ?? "(none)");
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        Diagnostics.Arrange("expected ECH result", expected ?? "(none)");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(Tls12Only(new TlsClientOptions(Insecure: true, Ech: mode)), OpenSslBuild)
                .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Act("handshake event count", events.Handshakes.Count());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        var echResult = Assert.ContainsSingle(events.Handshakes).EchResult;
        Diagnostics.Act("ECH result", echResult ?? "(none)");
        Diagnostics.Assert("ECH result", expected, echResult);
        Assert.AreEqual(expected, echResult);
        await connection.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    public static IEnumerable<object?[]> MeasuredEchLines =>
    [
        [new TlsClientOptions(Ech: "grease"), null, new[] { "ECH: will GREASE ClientHello" }],
        [Tls12Only(new TlsClientOptions(Ech: "grease")), null, new[] { "ECH: will GREASE ClientHello" }],
        [new TlsClientOptions(Ech: "grease", EchPublicName: "pn.test", EchConfigList: EchConfigListBase64()), null, new[] { "ECH: will GREASE ClientHello" }],
        [new TlsClientOptions(Ech: "true"), null, new[] { "ECH: requested but no ECHConfig available" }],
        [new TlsClientOptions(Ech: "true", EchPublicName: "pn.test"), null, new[] { "ECH: requested but no ECHConfig available" }],
        [new TlsClientOptions(Ech: "hard"), null, new[] { "ECH: requested but no ECHConfig available" }],
        [new TlsClientOptions(EchPublicName: "pn.test"), null, new[] { "ECH: requested but no ECHConfig available" }],
        [new TlsClientOptions(Ech: "true", EchConfigList: EchConfigListBase64()), null, new[] { "ECH: ECHConfig from command line" }],
        [new TlsClientOptions(EchConfigList: EchConfigListBase64()), null, new[] { "ECH: ECHConfig from command line" }],
        [new TlsClientOptions(Ech: "hard", EchPublicName: "pn.test", EchConfigList: EchConfigListBase64()), null, new[] { "ECH: ECHConfig from command line", "ECH: inner: 'curl.test', outer: 'pn.test'" }],
        [Tls12Only(new TlsClientOptions(Ech: "hard", EchConfigList: EchConfigListBase64())), null, new[] { "ECH: ECHConfig from command line" }],
        [new TlsClientOptions(Ech: "true", EchConfigList: "not base64!"), null, new[] { "ECH: SSL_ECH_set1_ech_config_list failed", "ECH: ECHConfig from command line" }],
        [new TlsClientOptions(Ech: "true", EchPublicName: "pn.test", EchConfigList: "AAA="), null, new[] { "ECH: SSL_ECH_set1_ech_config_list failed", "ECH: ECHConfig from command line" }],
        [new TlsClientOptions(Ech: "hard", EchConfigList: "not base64!"), null, new[] { "ECH: SSL_ECH_set1_ech_config_list failed" }],
        [new TlsClientOptions(Ech: "false", EchConfigList: "not base64!"), null, new[] { "ECH: SSL_ECH_set1_ech_config_list failed" }],
        [new TlsClientOptions(Ech: "true"), new FakeEchConfigListLookup(null), new[] { "ECH: requested but no ECHConfig available" }],
        [new TlsClientOptions(Ech: "true"), new FakeEchConfigListLookup(EchConfigListBytes("dns.test")), new[] { "ECH: ECHConfig from HTTPS RR", "ECH: imported ECHConfigList of length 61" }],
        [new TlsClientOptions(Ech: "hard"), new FakeEchConfigListLookup([0x00, 0x05, 0xfe]), new[] { "ECH: ECHConfig from HTTPS RR", "ECH: SSL_set1_ech_config_list failed" }],
        // Through --doh-url, measured with curl 8.21.0 and OpenSSL 4.0.0 against the recorder's DoH server (BL-1173).
        [new TlsClientOptions(Ech: "true"), new FakeEchConfigListLookup(Convert.FromBase64String(MeasuredDnsEchConfigList)), new[] { "ECH: ECHConfig from HTTPS RR", "ECH: imported ECHConfigList of length 64" }],
        [new TlsClientOptions(Ech: "hard"), new FakeEchConfigListLookup(Convert.FromBase64String(MeasuredDnsEchConfigList)), new[] { "ECH: ECHConfig from HTTPS RR", "ECH: imported ECHConfigList of length 64" }],
        [new TlsClientOptions(Ech: "true"), new FakeEchConfigListLookup([0x00, 0x04, 0xfe, 0x0d, 0x00, 0x00]), new[] { "ECH: ECHConfig from HTTPS RR", "ECH: SSL_set1_ech_config_list failed" }],
        [new TlsClientOptions(Ech: "hard"), new FakeEchConfigListLookup([0x00, 0x04, 0xfe, 0x0d, 0x00, 0x00]), new[] { "ECH: ECHConfig from HTTPS RR", "ECH: SSL_set1_ech_config_list failed" }],
        [new TlsClientOptions(Ech: "hard"), new FakeEchConfigListLookup(null), new[] { "ECH: requested but no ECHConfig available" }],
        [new TlsClientOptions(Ech: "grease"), new FakeEchConfigListLookup(Convert.FromBase64String(MeasuredDnsEchConfigList)), new[] { "ECH: will GREASE ClientHello" }],
        [new TlsClientOptions(Ech: "false"), null, Array.Empty<string>()],
    ];

    // The ECHConfigList `openssl ech -public_name example.com` made for the BL-1173 measurement
    // (X25519, HKDF-SHA256, AES-128-GCM): 64 bytes with its length prefix.
    private const string MeasuredDnsEchConfigList = "AD7+DQA6kwAgACC51/Ma8uuiPkyir5tuURRmE6scOVYKdlidZZStbLY0ZgAEAAEAAQALZXhhbXBsZS5jb20AAA==";

    [TestMethod]
    [DynamicData(nameof(MeasuredEchLines))]
    public async Task AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello(TlsClientOptions options, FakeEchConfigListLookup? lookup, string[] expected)
    {
        var events = new RecordingTransferEvents();
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await server.DisposeAsync();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", options.Ech ?? "(none)");
        Diagnostics.Arrange("EchPublicName", options.EchPublicName ?? "(none)");
        Diagnostics.Arrange("EchConfigList", options.EchConfigList ?? "(none)");
        Diagnostics.Arrange("DNS lookup", lookup is null ? "(none)" : "fake lookup");
        Diagnostics.Arrange("expected ECH lines", string.Join(" | ", expected));

        using (Diagnostics.Phase("TLS handshake"))
        {
            _ = await Provider(options with { Insecure = true }, OpenSslBuild, lookup)
                .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), EchHost, events, false, Http11, CancellationToken.None);
        }

        var echLines = events.Info.Where(line => line.StartsWith("ECH:", StringComparison.Ordinal)).ToArray();
        Diagnostics.Act("ECH lines", string.Join(" | ", echLines));
        Diagnostics.Assert("ECH lines", string.Join(" | ", expected), string.Join(" | ", echLines));
        CollectionAssert.AreEqual(expected, events.Info.Where(line => line.StartsWith("ECH:", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEchHardAndNoListAndACertCertificate_DisposesItAndFails()
    {
        var file = WriteFile("ech-client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        var (plaintext, stream) = Unanswered();
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("Ech", "hard");
        Diagnostics.Arrange("EchConfigList", "(none)");
        ArrangeCertificate("client certificate", s_clientCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(new TlsClientOptions(Insecure: true, Ech: "hard", ClientCertificate: file), SchannelBuild)
                .AuthenticateAsClientAsync(plaintext, EchHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Act("connection disposed", stream.IsDisposed);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("connection disposed", true, stream.IsDisposed);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.IsTrue(stream.IsDisposed);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DataRow("true")]
    [DataRow("hard")]
    public async Task AuthenticateAsClientAsync_WhenTheServerIgnoresTheEchOffer_FailsWithExit101(string mode)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            await using var sslStream = new SslStream(server);
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = s_serverCertificate,
                EnabledSslProtocols = SslProtocols.Tls13,
            });
            _ = await sslStream.ReadAsync(new byte[1]);
        });

        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("Ech", mode);
        Diagnostics.Arrange("EchConfigList", EchConfigListBase64());
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls13);
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(new TlsClientOptions(Insecure: true, Ech: mode, EchConfigList: EchConfigListBase64()), SchannelBuild)
                .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, events, false, Http11, CancellationToken.None);
        }

        var echLines = events.Info.Where(line => line.StartsWith("ECH:", StringComparison.Ordinal)).ToArray();
        ActConnectResult(result);
        Diagnostics.Act("ECH lines", string.Join(" | ", echLines));
        Diagnostics.Assert("exit code", CurlExitCode.EchRequired, result.ExitCode);
        Diagnostics.Assert("error message", "ECH required: error:0A0001A8:SSL routines::ech required", result.ErrorMessage);
        Diagnostics.Assert("ECH lines", "ECH: ECHConfig from command line | ECH: no retry_configs (rv = 1)", string.Join(" | ", echLines));
        Assert.AreEqual(CurlExitCode.EchRequired, result.ExitCode);
        Assert.AreEqual("ECH required: error:0A0001A8:SSL routines::ech required", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "ECH: ECHConfig from command line", "ECH: no retry_configs (rv = 1)" },
            events.Info.Where(line => line.StartsWith("ECH:", StringComparison.Ordinal)).ToArray());
        await IgnoreFailureAsync(serverTask);
    }

    // A rejecting server's retry_configs: the list, then the inner and outer names, before
    // exit 101 (measured with curl 8.21.0 and OpenSSL 4.0.0, BL-1171).
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheServerRejectsTheEchOfferWithRetryConfigs_WritesThemAndFailsWithExit101()
    {
        using var pki = new OcspTestPki();
        var retryConfigs = EchConfigListBytes("other.test");
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", "hard");
        Diagnostics.Arrange("EchConfigList", EchConfigListBase64());
        Diagnostics.Arrange("server TLS protocol", "TLS 1.3 test server");
        Diagnostics.Bytes("server retry_configs", retryConfigs);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithTestServerAsync(
                Provider(new TlsClientOptions(Insecure: true, Ech: "hard", EchConfigList: EchConfigListBase64()), OpenSslBuild),
                new Tls13TestServer(pki.LeafCredential) { EchRetryConfigs = retryConfigs },
                events);
        }

        ActConnectResult(result);
        Diagnostics.Act("ECH lines", string.Join(" | ", events.Info.Where(line => line.StartsWith("ECH:", StringComparison.Ordinal))));
        Diagnostics.Assert("exit code", CurlExitCode.EchRequired, result.ExitCode);
        Assert.AreEqual(CurlExitCode.EchRequired, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "ECH: ECHConfig from command line",
                "ECH: retry_configs " + Convert.ToBase64String(retryConfigs),
                "ECH: retry_configs for " + CertificateHost + " from " + EchPublicName + ", 424 -106",
            },
            events.Info.Where(line => line.StartsWith("ECH:", StringComparison.Ordinal)).ToArray());
    }

    // GREASE an ECH server answers with retry_configs: the result says so and the lines follow
    // it (measured with curl 8.21.0 and OpenSSL 4.0.0, BL-1171).
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEchGreaseAnsweredWithRetryConfigs_ReportsThemAfterTheResult()
    {
        using var pki = new OcspTestPki();
        var retryConfigs = EchConfigListBytes("other.test");
        var events = new RecordingTransferEvents();

        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ech", "grease");
        Diagnostics.Arrange("server TLS protocol", "TLS 1.3 test server");
        Diagnostics.Bytes("server retry_configs", retryConfigs);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithTestServerAsync(
                Provider(new TlsClientOptions(Insecure: true, Ech: "grease"), OpenSslBuild),
                new Tls13TestServer(pki.LeafCredential) { EchRetryConfigs = retryConfigs },
                events);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = events.Handshakes.Single();
        Diagnostics.Act("ECH result", handshake.EchResult ?? "(none)");
        Diagnostics.Assert("ECH result", "status is sent GREASE, got retry-configs, inner is NULL, outer is NULL", handshake.EchResult);
        Assert.AreEqual("status is sent GREASE, got retry-configs, inner is NULL, outer is NULL", handshake.EchResult);
        CollectionAssert.AreEqual(
            new[] { "ECH: retry_configs " + Convert.ToBase64String(retryConfigs), "ECH: retry_configs for NULL from NULL, 0 3" },
            handshake.EchRetryConfigLines.ToArray());
        await result.Connection!.DisposeAsync();
    }

    /// <summary>An ECHConfigList with one config: X25519, HKDF-SHA256 and AES-128-GCM, the test config ID.</summary>
    internal static byte[] EchConfigListBytes(string publicName, ushort kemId = 0x0020)
    {
        var name = Encoding.ASCII.GetBytes(publicName);
        byte[] publicKey = [0x09, .. new byte[31]];
        byte[] contents =
        [
            EchTestConfigId, (byte)(kemId >> 8), (byte)kemId,
            0x00, 0x20, .. publicKey,
            0x00, 0x04, 0x00, 0x01, 0x00, 0x01,
            0x00,
            (byte)name.Length, .. name,
            0x00, 0x00,
        ];
        byte[] config = [0xfe, 0x0d, (byte)(contents.Length >> 8), (byte)contents.Length, .. contents];
        return [(byte)(config.Length >> 8), (byte)config.Length, .. config];
    }

    private static string EchConfigListBase64() => Convert.ToBase64String(EchConfigListBytes(EchPublicName));

    // An outer hello: server_name is the public name, and the extension is an outer one for the test config.
    private static void AssertOffersTheTestConfig(ClientHello hello, string publicName)
    {
        var extension = ExtensionData(hello, TlsExtensionType.EncryptedClientHello);
        Assert.AreEqual(publicName, ServerNameOf(hello));
        Assert.AreEqual(0, extension[0]);
        Assert.AreEqual(EchTestConfigId, extension[5]);
        Assert.AreEqual(TlsExtensionType.EncryptedClientHello, ExtensionTypes(hello)[^1]);
    }

    // The host name of server_name: list length (2), name type (1), name length (2), name.
    private static string ServerNameOf(ClientHello hello) =>
        Encoding.ASCII.GetString(ExtensionData(hello, TlsExtensionType.ServerName)[5..]);
}
