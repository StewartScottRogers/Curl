using Curl.Networking.Fakes;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Captures the first record <see cref="HandBuiltTlsProvider" /> sends, with a test server
/// that records it and closes, and checks it is the platform curl's measured
/// <see cref="ClientHelloProfile" /> (BL-820, ADR-0140 "Default ClientHello"): the Schannel
/// build's for the Schannel build, the OpenSSL build's for the OpenSSL build, with the
/// target host's <c>server_name</c> and the offered ALPN, the options changing only its lists.
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    private const string ProfileHost = "example.com";

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_SendsTheBuildsProfileHelloByteForByte(bool matchesSchannelBuild)
    {
        var profile = ProfileOf(matchesSchannelBuild);
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "default TlsClientOptions");
        Diagnostics.Arrange("target host", ProfileHost);

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(), matchesSchannelBuild, ProfileHost, profile.ApplicationProtocols);
        }

        var hello = DecodeClientHello(record);
        var expected = profile with { SignatureAlgorithms = ClientHelloProfileMapping.CheckableSignatureAlgorithms(profile) };
        var rebuilt = expected.EncodeRecord(expected.Build(ProfileHost, hello.Random, hello.LegacySessionId, KeySharesOf(hello)));
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("legacy session id length", hello.LegacySessionId.Count());
        Diagnostics.Diff("ClientHello record", rebuilt, record);
        Diagnostics.Assert("legacy session id length", 32, hello.LegacySessionId.Count());
        CollectionAssert.AreEqual(rebuilt, record);
        Assert.HasCount(32, hello.LegacySessionId);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_CarriesTheTargetHostAndTheOfferedProtocols(bool matchesSchannelBuild)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("target host", "curl.test");
        Diagnostics.Arrange("offered protocols", string.Join(",", Http11));

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(), matchesSchannelBuild, "curl.test", Http11);
        }

        var hello = DecodeClientHello(record);
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("extension types", string.Join(",", ExtensionTypes(hello)));
        Diagnostics.Assert("extension order", string.Join(",", ProfileOf(matchesSchannelBuild).ExtensionOrder.ToArray()), string.Join(",", ExtensionTypes(hello)));
        CollectionAssert.AreEqual(ServerNameExtension.EncodeHostName("curl.test").Data, ExtensionData(hello, TlsExtensionType.ServerName));
        CollectionAssert.AreEqual(ApplicationLayerProtocolNegotiationExtension.Encode(Http11).Data, ExtensionData(hello, TlsExtensionType.ApplicationLayerProtocolNegotiation));
        CollectionAssert.AreEqual(ProfileOf(matchesSchannelBuild).ExtensionOrder.ToArray(), ExtensionTypes(hello));
    }

    [TestMethod]
    public void CheckableSignatureAlgorithms_KeepEverySchemeOfBothProfiles()
    {
        Diagnostics.Arrange("profiles", "Schannel and OpenSSL ClientHelloProfile");

        var schannelCheckable = ClientHelloProfileMapping.CheckableSignatureAlgorithms(ClientHelloProfile.Schannel).ToArray();
        var openSslCheckable = ClientHelloProfileMapping.CheckableSignatureAlgorithms(ClientHelloProfile.OpenSsl).ToArray();

        Diagnostics.Act("Schannel checkable scheme count", schannelCheckable.Length);
        Diagnostics.Act("OpenSSL checkable scheme count", openSslCheckable.Length);
        Diagnostics.Assert("Schannel scheme count", ClientHelloProfile.Schannel.SignatureAlgorithms.ToArray().Length, schannelCheckable.Length);
        Diagnostics.Assert("OpenSSL scheme count", ClientHelloProfile.OpenSsl.SignatureAlgorithms.ToArray().Length, openSslCheckable.Length);
        CollectionAssert.AreEqual(
            ClientHelloProfile.Schannel.SignatureAlgorithms.ToArray(),
            ClientHelloProfileMapping.CheckableSignatureAlgorithms(ClientHelloProfile.Schannel).ToArray());
        CollectionAssert.AreEqual(
            ClientHelloProfile.OpenSsl.SignatureAlgorithms.ToArray(),
            ClientHelloProfileMapping.CheckableSignatureAlgorithms(ClientHelloProfile.OpenSsl).ToArray());
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithNoAlpn_LeavesOnlyAlpnOut(bool matchesSchannelBuild)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("UseAlpn", false);

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(UseAlpn: false), matchesSchannelBuild, ProfileHost, Http11);
        }

        var expectedOrder = ProfileOf(matchesSchannelBuild).ExtensionOrder.Where(type => type != TlsExtensionType.ApplicationLayerProtocolNegotiation).ToArray();
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("extension types", string.Join(",", ExtensionTypes(DecodeClientHello(record))));
        Diagnostics.Assert("extension order", string.Join(",", expectedOrder), string.Join(",", ExtensionTypes(DecodeClientHello(record))));
        CollectionAssert.AreEqual(expectedOrder, ExtensionTypes(DecodeClientHello(record)));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertStatusInTheOpenSslBuild_AddsStatusRequestAfterSupportedGroups()
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("RequireCertificateStatus", true);

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(RequireCertificateStatus: true), OpenSslBuild, ProfileHost, Http11);
        }

        var types = ExtensionTypes(DecodeClientHello(record)).ToList();
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("status_request index", types.IndexOf(TlsExtensionType.StatusRequest));
        Diagnostics.Assert("status_request index", types.IndexOf(TlsExtensionType.SupportedGroups) + 1, types.IndexOf(TlsExtensionType.StatusRequest));
        Assert.AreEqual(types.IndexOf(TlsExtensionType.SupportedGroups) + 1, types.IndexOf(TlsExtensionType.StatusRequest));
        CollectionAssert.AreEqual(ClientHelloProfile.OpenSsl.ExtensionOrder.ToArray(), types.Where(type => type != TlsExtensionType.StatusRequest).ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertStatusInTheSchannelBuild_KeepsTheProfilesOrder()
    {
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("RequireCertificateStatus", true);

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(RequireCertificateStatus: true), SchannelBuild, ProfileHost, Http11);
        }

        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("extension types", string.Join(",", ExtensionTypes(DecodeClientHello(record))));
        Diagnostics.Assert("extension order", string.Join(",", ClientHelloProfile.Schannel.ExtensionOrder.ToArray()), string.Join(",", ExtensionTypes(DecodeClientHello(record))));
        CollectionAssert.AreEqual(ClientHelloProfile.Schannel.ExtensionOrder.ToArray(), ExtensionTypes(DecodeClientHello(record)));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithTls13CiphersInTheOpenSslBuild_ChangesOnlyTheSuites()
    {
        var options = new TlsClientOptions(Tls13Ciphers: "TLS_AES_128_GCM_SHA256");
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Tls13Ciphers", "TLS_AES_128_GCM_SHA256");

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(options, OpenSslBuild, ProfileHost, Http11);
        }

        var hello = DecodeClientHello(record);
        var expectedSuites = new ushort[] { 0x1301 }.Concat(ClientHelloProfile.OpenSsl.CipherSuites.Where(suite => suite >> 8 != 0x13)).ToArray();
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("cipher suites", string.Join(",", hello.CipherSuites.ToArray().Select(suite => suite.ToString("x4"))));
        Diagnostics.Assert("cipher suite count", expectedSuites.Length, hello.CipherSuites.ToArray().Length);
        CollectionAssert.AreEqual(
            new ushort[] { 0x1301 }.Concat(ClientHelloProfile.OpenSsl.CipherSuites.Where(suite => suite >> 8 != 0x13)).ToArray(),
            hello.CipherSuites.ToArray());
        CollectionAssert.AreEqual(ClientHelloProfile.OpenSsl.ExtensionOrder.ToArray(), ExtensionTypes(hello));
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithATls13Minimum_OffersOnlyTls13InTheProfilesOrder(bool matchesSchannelBuild)
    {
        var profile = ProfileOf(matchesSchannelBuild);
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("MinimumVersion", TlsVersion.Tls13);

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(new TlsClientOptions(MinimumVersion: TlsVersion.Tls13), matchesSchannelBuild, ProfileHost, Http11);
        }

        var hello = DecodeClientHello(record);
        var expectedSuites = profile.CipherSuites.Where(suite => suite >> 8 == 0x13).ToArray();
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("cipher suites", string.Join(",", hello.CipherSuites.ToArray().Select(suite => suite.ToString("x4"))));
        Diagnostics.Assert("cipher suite count", expectedSuites.Length, hello.CipherSuites.ToArray().Length);
        CollectionAssert.AreEqual(profile.CipherSuites.Where(suite => suite >> 8 == 0x13).ToArray(), hello.CipherSuites.ToArray());
        CollectionAssert.AreEqual(SupportedVersionsExtension.EncodeOffered([0x0304]).Data, ExtensionData(hello, TlsExtensionType.SupportedVersions));
        CollectionAssert.AreEqual(profile.ExtensionOrder.ToArray(), ExtensionTypes(hello));
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithATls12Ceiling_OffersTheProfilesTls12Lists(bool matchesSchannelBuild)
    {
        var profile = ProfileOf(matchesSchannelBuild);
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("MaximumVersion", "TLS 1.2 ceiling");

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(Tls12Only(new TlsClientOptions()), matchesSchannelBuild, ProfileHost, Http11);
        }

        var hello = DecodeClientHello(record);
        var expectedEncryptThenMac = profile.ExtensionOrder.Contains(TlsExtensionType.EncryptThenMac);
        var actualEncryptThenMac = hello.Extensions.Any(extension => extension.Type == TlsExtensionType.EncryptThenMac);
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("cipher suites", string.Join(",", hello.CipherSuites.ToArray().Select(suite => suite.ToString("x4"))));
        Diagnostics.Assert("encrypt_then_mac offered", expectedEncryptThenMac, actualEncryptThenMac);
        CollectionAssert.AreEqual(profile.CipherSuites.Where(suite => suite >> 8 != 0x13).ToArray(), hello.CipherSuites.ToArray());
        CollectionAssert.AreEqual(
            SupportedGroupsExtension.Encode([.. profile.SupportedGroups.Where(TlsNamedGroup.IsTls12EcdheGroup)]).Data,
            ExtensionData(hello, TlsExtensionType.SupportedGroups));
        CollectionAssert.AreEqual(
            SignatureAlgorithmsExtension.Encode([.. profile.SignatureAlgorithms.Where(TlsSignatureScheme.IsTls12Scheme)]).Data,
            ExtensionData(hello, TlsExtensionType.SignatureAlgorithms));
        Assert.AreEqual(profile.ExtensionOrder.Contains(TlsExtensionType.EncryptThenMac), hello.Extensions.Any(extension => extension.Type == TlsExtensionType.EncryptThenMac));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task AuthenticateAsClientAsync_WithADheDssCipher_OffersItWithTheDsaSignatureSchemes(bool tls12Ceiling)
    {
        var options = new TlsClientOptions(Ciphers: "DHE-DSS-AES128-GCM-SHA256");
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("Ciphers", "DHE-DSS-AES128-GCM-SHA256");
        Diagnostics.Arrange("TLS 1.2 ceiling", tls12Ceiling);

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(tls12Ceiling ? Tls12Only(options) : options, OpenSslBuild, ProfileHost, Http11);
        }

        var hello = DecodeClientHello(record);
        var offeredSchemes = SignatureAlgorithmsExtension.Decode(ExtensionData(hello, TlsExtensionType.SignatureAlgorithms)).Value.ToArray();
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("cipher suites", string.Join(",", hello.CipherSuites.ToArray().Select(suite => suite.ToString("x4"))));
        Diagnostics.Act("offered signature schemes", string.Join(",", offeredSchemes.Select(scheme => scheme.ToString())));
        Diagnostics.Assert("offers DHE-DSS-AES128-GCM-SHA256 (0x00a2)", true, hello.CipherSuites.ToArray().Contains((ushort)0x00a2));
        Assert.Contains((ushort)0x00a2, hello.CipherSuites.ToArray());
        CollectionAssert.IsSubsetOf(
            new[] { TlsSignatureScheme.DsaSha224, TlsSignatureScheme.DsaSha256, TlsSignatureScheme.DsaSha384, TlsSignatureScheme.DsaSha512 },
            offeredSchemes);
    }

    // Measured with Record-CurlExchange.ps1 -Script against https://localhost (BL-941): the
    // Windows reference build with --tls-max 1.2 and --tls-max 1.0, and Ubuntu's OpenSSL build
    // with --tls-max 1.2 (its --tls-max 1.0 sends a protocol_version alert, not a hello).
    // Each extension is its type and its data, in the order sent.
    private static readonly (TlsExtensionType Type, string Data)[] MeasuredSchannelTls12Extensions =
    [
        (TlsExtensionType.ServerName, "000c0000096c6f63616c686f7374"),
        (TlsExtensionType.StatusRequest, "0100000000"),
        (TlsExtensionType.SupportedGroups, "0006001d00170018"),
        (TlsExtensionType.EcPointFormats, "0100"),
        (TlsExtensionType.SignatureAlgorithms, "0018080408050806040105010201040305030203020206010603"),
        (TlsExtensionType.SessionTicket, string.Empty),
        (TlsExtensionType.ApplicationLayerProtocolNegotiation, "000908687474702f312e31"),
        (TlsExtensionType.ExtendedMasterSecret, string.Empty),
        (TlsExtensionType.RenegotiationInfo, "00"),
    ];

    private static readonly (TlsExtensionType Type, string Data)[] MeasuredOpenSslTls12Extensions =
    [
        (TlsExtensionType.RenegotiationInfo, "00"),
        (TlsExtensionType.ServerName, "000c0000096c6f63616c686f7374"),
        (TlsExtensionType.EcPointFormats, "03000102"),
        (TlsExtensionType.SupportedGroups, "000a001d0017001e00180019"),
        (TlsExtensionType.ApplicationLayerProtocolNegotiation, "000c02683208687474702f312e31"),
        (TlsExtensionType.EncryptThenMac, string.Empty),
        (TlsExtensionType.ExtendedMasterSecret, string.Empty),
        (TlsExtensionType.SignatureAlgorithms, "0028040305030603080708080809080a080b080408050806040105010601030303010302040205020602"),
    ];

    [TestMethod]
    [DataRow(SchannelBuild, TlsVersion.Tls12)]
    [DataRow(SchannelBuild, TlsVersion.Tls10)]
    [DataRow(OpenSslBuild, TlsVersion.Tls12)]
    public async Task AuthenticateAsClientAsync_BelowATls13Ceiling_SendsTheMeasuredExtensionsInTheMeasuredOrder(bool matchesSchannelBuild, TlsVersion ceiling)
    {
        var profile = ProfileOf(matchesSchannelBuild);
        var measured = (matchesSchannelBuild ? MeasuredSchannelTls12Extensions : MeasuredOpenSslTls12Extensions)
            .Where(extension => ceiling == TlsVersion.Tls12 || extension.Type != TlsExtensionType.SignatureAlgorithms)
            .ToArray();
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("MaximumVersion", ceiling);
        Diagnostics.Arrange("measured extension count", measured.Length);

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(
                new TlsClientOptions { MaximumVersion = ceiling }, matchesSchannelBuild, "localhost", profile.ApplicationProtocols);
        }

        var hello = DecodeClientHello(record);
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("extension types", string.Join(",", ExtensionTypes(hello)));
        Diagnostics.Assert("extension order", string.Join(",", measured.Select(extension => extension.Type)), string.Join(",", ExtensionTypes(hello)));
        CollectionAssert.AreEqual(measured.Select(extension => extension.Type).ToArray(), ExtensionTypes(hello));
        foreach (var (type, data) in measured)
        {
            // ADR-0235 decision 1: the signature schemes the client cannot check are left out.
            var expected = type == TlsExtensionType.SignatureAlgorithms
                ? SignatureAlgorithmsExtension.Encode([.. SignatureAlgorithmsExtension.Decode(Convert.FromHexString(data)).Value.Where(TlsSignatureScheme.IsTls12Scheme)]).Data
                : Convert.FromHexString(data);
            Diagnostics.Diff($"{type} extension data", expected, ExtensionData(hello, type));
            CollectionAssert.AreEqual(expected, ExtensionData(hello, type), $"{type}");
        }
    }

    // Measured with Record-CurlExchange.ps1 -Script (read, close) on 2026-10-02 (BL-1152): the
    // Windows reference build's --tls-max 1.2 hello is in a 3.3 record and its --tls-max 1.0
    // hello in a 3.1 one; Ubuntu's OpenSSL build's --tls-max 1.2 hello is in a 3.1 record.
    // Every one offers an empty legacy session ID.
    [TestMethod]
    [DataRow(SchannelBuild, TlsVersion.Tls12, 0x0303)]
    [DataRow(SchannelBuild, TlsVersion.Tls10, 0x0301)]
    [DataRow(OpenSslBuild, TlsVersion.Tls12, 0x0301)]
    public async Task AuthenticateAsClientAsync_BelowATls13Ceiling_SendsTheMeasuredRecordVersionAndAnEmptySessionId(bool matchesSchannelBuild, TlsVersion ceiling, int recordVersion)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("MaximumVersion", ceiling);
        Diagnostics.Arrange("expected record version", $"0x{recordVersion:x4}");

        byte[] record;
        using (Diagnostics.Phase("TLS handshake"))
        {
            record = await CaptureClientHelloAsync(
                new TlsClientOptions { MaximumVersion = ceiling }, matchesSchannelBuild, "localhost", ProfileOf(matchesSchannelBuild).ApplicationProtocols);
        }

        var actualRecordVersion = (record[1] << 8) | record[2];
        Diagnostics.Bytes("captured ClientHello record", record);
        Diagnostics.Act("record version", $"0x{actualRecordVersion:x4}");
        Diagnostics.Assert("record version", recordVersion, actualRecordVersion);
        Diagnostics.Assert("legacy session id length", 0, DecodeClientHello(record).LegacySessionId.Count());
        Assert.AreEqual(recordVersion, (record[1] << 8) | record[2]);
        Assert.IsEmpty(DecodeClientHello(record).LegacySessionId);
    }

    private static ClientHelloProfile ProfileOf(bool matchesSchannelBuild) =>
        matchesSchannelBuild ? ClientHelloProfile.Schannel : ClientHelloProfile.OpenSsl;

    // Runs the provider against a server that records the first record it receives and closes.
    private static async Task<byte[]> CaptureClientHelloAsync(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        string targetHost,
        IReadOnlyList<string> applicationProtocols,
        IEchConfigListLookup? echConfigs = null)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            var header = new byte[5];
            await server.ReadExactlyAsync(header);
            var body = new byte[(header[3] << 8) | header[4]];
            await server.ReadExactlyAsync(body);
            await server.DisposeAsync();
            return (byte[])[.. header, .. body];
        });

        var result = await Provider(options with { Insecure = true }, matchesSchannelBuild, echConfigs).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), targetHost, new RecordingTransferEvents(), false, applicationProtocols, CancellationToken.None);

        Assert.AreNotEqual(Protocol.Abstractions.CurlExitCode.Ok, result.ExitCode);
        return await serverTask;
    }

    // The record's handshake message, past the 5-byte record header and 4-byte handshake header.
    private static ClientHello DecodeClientHello(byte[] record) =>
        ClientHello.Decode(record[9..]).Value;

    private static IReadOnlyList<KeyShareEntry> KeySharesOf(ClientHello hello) =>
        KeyShareExtension.DecodeClientShares(ExtensionData(hello, TlsExtensionType.KeyShare)).Value;

    private static byte[] ExtensionData(ClientHello hello, TlsExtensionType type) =>
        hello.Extensions.Single(extension => extension.Type == type).Data;

    private static TlsExtensionType[] ExtensionTypes(ClientHello hello) =>
        [.. hello.Extensions.Select(extension => extension.Type)];
}
