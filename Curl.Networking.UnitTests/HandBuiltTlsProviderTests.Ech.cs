using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Networking.Fakes;
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
        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(Ech: "grease", EchConfigList: EchConfigListBase64()), OpenSslBuild, EchHost, Http11));

        Assert.AreEqual(TlsExtensionType.EncryptedClientHello, ExtensionTypes(hello)[^1]);
        Assert.AreEqual(EchHost, ServerNameOf(hello));
        Assert.AreEqual(0, ExtensionData(hello, TlsExtensionType.EncryptedClientHello)[0]);
    }

    [TestMethod]
    [DataRow("false")]
    [DataRow(null)]
    [DataRow("bogus")]
    public async Task AuthenticateAsClientAsync_WithEchOff_SendsNoEchExtension(string? mode)
    {
        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(Ech: mode, EchPublicName: "pn.test"), OpenSslBuild, EchHost, Http11));

        Assert.IsFalse(ExtensionTypes(hello).Contains(TlsExtensionType.EncryptedClientHello));
        Assert.AreEqual(EchHost, ServerNameOf(hello));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEchFalseAndAList_SendsNoEchExtension()
    {
        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(Ech: "false", EchConfigList: EchConfigListBase64()), OpenSslBuild, EchHost, Http11));

        Assert.IsFalse(ExtensionTypes(hello).Contains(TlsExtensionType.EncryptedClientHello));
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("hard")]
    [DataRow(null)]
    public async Task AuthenticateAsClientAsync_WithAnEclList_SendsTheOuterHelloForItsConfig(string? mode)
    {
        var lookup = new FakeEchConfigListLookup(EchConfigListBytes("dns.test"));

        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(Ech: mode, EchConfigList: EchConfigListBase64()), SchannelBuild, EchHost, Http11, lookup));

        AssertOffersTheTestConfig(hello, EchPublicName);
        Assert.IsEmpty(lookup.Asked);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAPublicName_NamesItInTheOuterHello()
    {
        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(Ech: "true", EchPublicName: "pn.test", EchConfigList: EchConfigListBase64()), OpenSslBuild, EchHost, Http11));

        AssertOffersTheTestConfig(hello, "pn.test");
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("hard")]
    public async Task AuthenticateAsClientAsync_WithoutAnEclList_OffersTheHostsListFromDns(string mode)
    {
        var lookup = new FakeEchConfigListLookup(EchConfigListBytes("dns.test"));

        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(Ech: mode), OpenSslBuild, EchHost, Http11, lookup));

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
        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(Ech: "true", EchConfigList: eclList), OpenSslBuild, EchHost, Http11, lookup));

        Assert.IsFalse(ExtensionTypes(hello).Contains(TlsExtensionType.EncryptedClientHello));
        Assert.AreEqual(EchHost, ServerNameOf(hello));
    }

    [TestMethod]
    [DynamicData(nameof(UnusableEchSources))]
    public async Task AuthenticateAsClientAsync_WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte(string? eclList, FakeEchConfigListLookup? lookup)
    {
        var (plaintext, stream) = Unanswered();

        var result = await Provider(new TlsClientOptions(Insecure: true, Ech: "hard", EchConfigList: eclList), OpenSslBuild, lookup)
            .AuthenticateAsClientAsync(plaintext, EchHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("SSL connect error", result.ErrorMessage);
        Assert.IsTrue(stream.IsDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEchHardBelowTls13_FailsWithExit35()
    {
        var (plaintext, _) = Unanswered();

        var result = await Provider(Tls12Only(new TlsClientOptions(Insecure: true, Ech: "hard", EchConfigList: EchConfigListBase64())), SchannelBuild)
            .AuthenticateAsClientAsync(plaintext, EchHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
    }

    [TestMethod]
    [DataRow("grease")]
    [DataRow("true")]
    public async Task AuthenticateAsClientAsync_WithEchBelowTls13_SendsAPlainTls12Hello(string mode)
    {
        var record = await CaptureClientHelloAsync(Tls12Only(new TlsClientOptions(Ech: mode, EchConfigList: EchConfigListBase64())), OpenSslBuild, EchHost, Http11);

        Assert.IsFalse(ExtensionTypes(DecodeClientHello(record)).Contains(TlsExtensionType.EncryptedClientHello));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEchHardAndNoListAndACertCertificate_DisposesItAndFails()
    {
        var file = WriteFile("ech-client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        var (plaintext, stream) = Unanswered();

        var result = await Provider(new TlsClientOptions(Insecure: true, Ech: "hard", ClientCertificate: file), SchannelBuild)
            .AuthenticateAsClientAsync(plaintext, EchHost, CancellationToken.None);

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

        var result = await Provider(new TlsClientOptions(Insecure: true, Ech: mode, EchConfigList: EchConfigListBase64()), OpenSslBuild)
            .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.EchRequired, result.ExitCode);
        Assert.AreEqual("ECH attempted but failed", result.ErrorMessage);
        await IgnoreFailureAsync(serverTask);
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
