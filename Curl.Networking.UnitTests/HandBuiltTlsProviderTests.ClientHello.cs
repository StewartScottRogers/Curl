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

        var record = await CaptureClientHelloAsync(new TlsClientOptions(), matchesSchannelBuild, ProfileHost, profile.ApplicationProtocols);

        var hello = DecodeClientHello(record);
        var expected = profile with { SignatureAlgorithms = ClientHelloProfileMapping.CheckableSignatureAlgorithms(profile) };
        var rebuilt = expected.EncodeRecord(expected.Build(ProfileHost, hello.Random, hello.LegacySessionId, KeySharesOf(hello)));
        CollectionAssert.AreEqual(rebuilt, record);
        Assert.HasCount(32, hello.LegacySessionId);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_CarriesTheTargetHostAndTheOfferedProtocols(bool matchesSchannelBuild)
    {
        var record = await CaptureClientHelloAsync(new TlsClientOptions(), matchesSchannelBuild, "curl.test", Http11);

        var hello = DecodeClientHello(record);
        CollectionAssert.AreEqual(ServerNameExtension.EncodeHostName("curl.test").Data, ExtensionData(hello, TlsExtensionType.ServerName));
        CollectionAssert.AreEqual(ApplicationLayerProtocolNegotiationExtension.Encode(Http11).Data, ExtensionData(hello, TlsExtensionType.ApplicationLayerProtocolNegotiation));
        CollectionAssert.AreEqual(ProfileOf(matchesSchannelBuild).ExtensionOrder.ToArray(), ExtensionTypes(hello));
    }

    [TestMethod]
    public void CheckableSignatureAlgorithms_KeepEverySchemeOfBothProfiles()
    {
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
        var record = await CaptureClientHelloAsync(new TlsClientOptions(UseAlpn: false), matchesSchannelBuild, ProfileHost, Http11);

        var expectedOrder = ProfileOf(matchesSchannelBuild).ExtensionOrder.Where(type => type != TlsExtensionType.ApplicationLayerProtocolNegotiation).ToArray();
        CollectionAssert.AreEqual(expectedOrder, ExtensionTypes(DecodeClientHello(record)));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertStatusInTheOpenSslBuild_AddsStatusRequestAfterSupportedGroups()
    {
        var record = await CaptureClientHelloAsync(new TlsClientOptions(RequireCertificateStatus: true), OpenSslBuild, ProfileHost, Http11);

        var types = ExtensionTypes(DecodeClientHello(record)).ToList();
        Assert.AreEqual(types.IndexOf(TlsExtensionType.SupportedGroups) + 1, types.IndexOf(TlsExtensionType.StatusRequest));
        CollectionAssert.AreEqual(ClientHelloProfile.OpenSsl.ExtensionOrder.ToArray(), types.Where(type => type != TlsExtensionType.StatusRequest).ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertStatusInTheSchannelBuild_KeepsTheProfilesOrder()
    {
        var record = await CaptureClientHelloAsync(new TlsClientOptions(RequireCertificateStatus: true), SchannelBuild, ProfileHost, Http11);

        CollectionAssert.AreEqual(ClientHelloProfile.Schannel.ExtensionOrder.ToArray(), ExtensionTypes(DecodeClientHello(record)));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithTls13CiphersInTheOpenSslBuild_ChangesOnlyTheSuites()
    {
        var options = new TlsClientOptions(Tls13Ciphers: "TLS_AES_128_GCM_SHA256");

        var hello = DecodeClientHello(await CaptureClientHelloAsync(options, OpenSslBuild, ProfileHost, Http11));

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

        var hello = DecodeClientHello(await CaptureClientHelloAsync(new TlsClientOptions(MinimumVersion: TlsVersion.Tls13), matchesSchannelBuild, ProfileHost, Http11));

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

        var hello = DecodeClientHello(await CaptureClientHelloAsync(Tls12Only(new TlsClientOptions()), matchesSchannelBuild, ProfileHost, Http11));

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

        var hello = DecodeClientHello(await CaptureClientHelloAsync(tls12Ceiling ? Tls12Only(options) : options, OpenSslBuild, ProfileHost, Http11));

        Assert.Contains((ushort)0x00a2, hello.CipherSuites.ToArray());
        var offeredSchemes = SignatureAlgorithmsExtension.Decode(ExtensionData(hello, TlsExtensionType.SignatureAlgorithms)).Value.ToArray();
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

        var hello = DecodeClientHello(await CaptureClientHelloAsync(
            new TlsClientOptions { MaximumVersion = ceiling }, matchesSchannelBuild, "localhost", profile.ApplicationProtocols));

        CollectionAssert.AreEqual(measured.Select(extension => extension.Type).ToArray(), ExtensionTypes(hello));
        foreach (var (type, data) in measured)
        {
            // ADR-0235 decision 1: the signature schemes the client cannot check are left out.
            var expected = type == TlsExtensionType.SignatureAlgorithms
                ? SignatureAlgorithmsExtension.Encode([.. SignatureAlgorithmsExtension.Decode(Convert.FromHexString(data)).Value.Where(TlsSignatureScheme.IsTls12Scheme)]).Data
                : Convert.FromHexString(data);
            CollectionAssert.AreEqual(expected, ExtensionData(hello, type), $"{type}");
        }
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
