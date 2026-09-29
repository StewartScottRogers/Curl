using Curl.Cryptography;
using static Curl.Tls.EchTestFrontEnd;
using static Curl.Tls.HandshakeDriver;

namespace Curl.Tls;

/// <summary>
/// Encrypted Client Hello (RFC 9849) in the TLS 1.3 client against an in-memory
/// client-facing server (<see cref="EchTestFrontEnd" />) and backend
/// (<see cref="Tls13TestServer" />): accepted, with and without a HelloRetryRequest; rejected,
/// with and without retry configs; and GREASE, its bytes pinned for fixed randoms.
/// </summary>
[TestClass]
public sealed class Tls13EncryptedClientHelloTests
{
    private static readonly IReadOnlyList<TlsExtensionType> OrderWithEch =
        [.. Tls13ClientSettings.DefaultExtensionOrder, TlsExtensionType.EncryptedClientHello];

    [TestMethod]
    [DataRow(0x0020, 0x0001, DisplayName = "X25519 with AES-128-GCM")]
    [DataRow(0x0020, 0x0002, DisplayName = "X25519 with AES-256-GCM")]
    [DataRow(0x0020, 0x0003, DisplayName = "X25519 with ChaCha20-Poly1305")]
    [DataRow(0x0010, 0x0001, DisplayName = "P-256 with AES-128-GCM")]
    public void AnAcceptedHandshakeCompletesAndTheServerSeesTheInnerName(int kemId, int aeadId)
    {
        EchCipherSuite suite = new(1, (ushort)aeadId);
        EchTestConfig config = kemId == (int)HpkeKem.DhkemP256HkdfSha256 ? EchTestConfig.P256(suite) : EchTestConfig.X25519(suite);
        using EchTestFrontEnd frontEnd = new(config);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { ConfirmEch = true };
        RecordingCertificateVerifier verifier = new();
        using Tls13ClientHandshake client = Client(EchSettings(config), verifier);

        Tls13HandshakeOutput output = Run(client, server, forwardClientHello: frontEnd.Decrypt);

        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        Assert.IsTrue(client.EncryptedClientHelloOffered);
        Assert.IsTrue(client.EncryptedClientHelloAccepted);
        Assert.AreEqual(EchTestConfig.DefaultPublicName, ServerNameOf(frontEnd.OuterHellos[0]));
        Assert.AreEqual("localhost", ServerNameOf(frontEnd.InnerHellos[0]));
        Assert.AreEqual("localhost", verifier.Presented.Single().HostName);
        CollectionAssert.AreEqual(server.ClientApplicationTrafficSecret, output.SecretsInstalled[1].Secret);
        Assert.IsNull(client.EncryptedClientHelloRetryConfigs);
    }

    [TestMethod]
    public void TheInnerHelloIsPaddedSoNamesUpToTheMaximumLengthLookAlike()
    {
        int shortName = EncodedInnerLength(EchSettings(EchTestConfig.X25519(maximumNameLength: 32)) with { ServerName = "a.example" });
        int longName = EncodedInnerLength(EchSettings(EchTestConfig.X25519(maximumNameLength: 32)) with { ServerName = "a-much-longer-name.example" });

        Assert.AreEqual(shortName, longName);
    }

    [TestMethod]
    public void AnInnerHelloWithoutANameIsPaddedToTheLengthOfOneWithAName()
    {
        int withName = EncodedInnerLength(EchSettings(EchTestConfig.X25519(maximumNameLength: 200)) with { ServerName = "a.example" });
        int withoutName = EncodedInnerLength(EchSettings(EchTestConfig.X25519(maximumNameLength: 200)) with { ServerName = null });

        // RFC 9849 section 6.1.3: the 9 extra bytes stand in for the server_name extension's own framing.
        Assert.AreEqual(withName, withoutName);
    }

    [TestMethod]
    public void TheInnerHelloSharesTheOuterHellosLegacySessionId()
    {
        EchTestConfig config = EchTestConfig.X25519();
        using EchTestFrontEnd frontEnd = new(config);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { ConfirmEch = true };
        using Tls13ClientHandshake client = Client(EchSettings(config) with { SendLegacySessionId = true });

        Tls13HandshakeOutput output = Run(client, server, forwardClientHello: frontEnd.Decrypt);

        Assert.IsTrue(output.IsComplete);
        Assert.HasCount(32, frontEnd.OuterHellos[0].LegacySessionId);
        CollectionAssert.AreEqual(frontEnd.OuterHellos[0].LegacySessionId, frontEnd.InnerHellos[0].LegacySessionId);
    }

    [TestMethod]
    public void TheInnerHelloOffersTls13AloneWhenTheOuterOneOffersLowerVersionsToo()
    {
        EchTestConfig config = EchTestConfig.X25519();
        using EchTestFrontEnd frontEnd = new(config);
        using Tls13ClientHandshake client = Client(EchSettings(config) with { LowerVersions = Tls12PipeDriver.DefaultSettings });

        frontEnd.Decrypt(client.Start().BytesToSend[0].Bytes);

        CollectionAssert.AreEqual(new ushort[] { 0x0304 }, OfferedVersions(frontEnd.InnerHellos[0]).ToArray());
        CollectionAssert.Contains(OfferedVersions(frontEnd.OuterHellos[0]).ToList(), (ushort)0x0303);
    }

    [TestMethod]
    public void AnAcceptedHandshakeCompletesThroughAHelloRetryRequest()
    {
        EchTestConfig config = EchTestConfig.X25519();
        using EchTestFrontEnd frontEnd = new(config);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { ConfirmEch = true, Group = TlsNamedGroup.Secp256r1, RetryCookie = [7, 7] };
        using Tls13ClientHandshake client = Client(EchSettings(config) with { SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1] });

        Tls13HandshakeOutput output = Run(client, server, forwardClientHello: frontEnd.Decrypt);

        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        Assert.IsTrue(server.SentHelloRetryRequest);
        Assert.IsTrue(client.EncryptedClientHelloAccepted);
        Assert.HasCount(2, frontEnd.InnerHellos);
        CollectionAssert.AreEqual(frontEnd.InnerHellos[0].Random, frontEnd.InnerHellos[1].Random);
    }

    [TestMethod]
    public void AServerHelloThatDoesNotConfirmWhatItsHelloRetryRequestConfirmedIsAnIllegalParameter()
    {
        EchTestConfig config = EchTestConfig.X25519();
        using EchTestFrontEnd frontEnd = new(config);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { ConfirmEch = true, Group = TlsNamedGroup.Secp256r1 };
        using Tls13ClientHandshake client = Client(EchSettings(config) with { SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1] });

        Tls13HandshakeOutput output = Run(client, server, replaceServerHello: FlipLastRandomByte, forwardClientHello: frontEnd.Decrypt);

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, output.Failure!.Alert);
        Assert.IsFalse(client.EncryptedClientHelloAccepted);
    }

    [TestMethod]
    [DataRow(7)]
    [DataRow(9)]
    public void AHelloRetryRequestConfirmationOfTheWrongLengthIsADecodeError(int length)
    {
        EchTestConfig config = EchTestConfig.X25519();
        using EchTestFrontEnd frontEnd = new(config);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { ConfirmEch = true, Group = TlsNamedGroup.Secp256r1 };
        using Tls13ClientHandshake client = Client(EchSettings(config) with { SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1] });

        Tls13HandshakeOutput output = Run(client, server, forwardClientHello: frontEnd.Decrypt, replaceRetryRequest: retry => ReplaceEchExtension(retry, new byte[length]));

        Assert.AreEqual(TlsAlertDescription.DecodeError, output.Failure!.Alert);
    }

    [TestMethod]
    public void AWrongHelloRetryRequestConfirmationRejectsEch()
    {
        EchTestConfig config = EchTestConfig.X25519();
        using EchTestFrontEnd frontEnd = new(config);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { ConfirmEch = true, Group = TlsNamedGroup.Secp256r1 };
        using Tls13ClientHandshake client = Client(EchSettings(config) with { SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1] });

        Tls13HandshakeOutput output = Run(client, server, forwardClientHello: frontEnd.Decrypt, replaceRetryRequest: retry => ReplaceEchExtension(retry, new byte[8]));

        Assert.IsNotNull(output.Failure);
        Assert.IsFalse(client.EncryptedClientHelloAccepted);
    }

    [TestMethod]
    public void ARejectionWithRetryConfigsEndsWithEchRequiredAndHandsThemBack()
    {
        EchTestConfig config = EchTestConfig.X25519();
        byte[] retryConfigs = EchTestConfig.X25519(maximumNameLength: 64).ConfigList;
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { EchRetryConfigs = retryConfigs };
        RecordingCertificateVerifier verifier = new();
        using Tls13ClientHandshake client = Client(EchSettings(config), verifier);

        Tls13HandshakeOutput output = Run(client, server);

        Assert.AreEqual(TlsAlertDescription.EchRequired, output.Failure!.Alert);
        Assert.IsFalse(output.IsComplete);
        Assert.IsEmpty(output.BytesToSend);
        Assert.IsTrue(client.EncryptedClientHelloOffered);
        Assert.IsFalse(client.EncryptedClientHelloAccepted);
        Assert.AreEqual(EchTestConfig.DefaultPublicName, verifier.Presented.Single().HostName);
        CollectionAssert.AreEqual(retryConfigs, client.EncryptedClientHelloRetryConfigs!.Encoded);
        Assert.AreEqual(64, client.EncryptedClientHelloRetryConfigs.SupportedConfig!.MaximumNameLength);
    }

    [TestMethod]
    public void ARejectionWithoutRetryConfigsEndsWithEchRequired()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519());
        using Tls13ClientHandshake client = Client(EchSettings(EchTestConfig.X25519()));

        Tls13HandshakeOutput output = Run(client, server);

        Assert.AreEqual(TlsAlertDescription.EchRequired, output.Failure!.Alert);
        Assert.IsNull(client.EncryptedClientHelloRetryConfigs);
    }

    [TestMethod]
    public void ARejectionThroughAHelloRetryRequestEndsWithEchRequired()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Group = TlsNamedGroup.Secp256r1 };
        RecordingCertificateVerifier verifier = new();
        using Tls13ClientHandshake client = Client(EchSettings(EchTestConfig.X25519()) with { SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1] }, verifier);

        Tls13HandshakeOutput output = Run(client, server);

        Assert.IsTrue(server.SentHelloRetryRequest);
        Assert.AreEqual(TlsAlertDescription.EchRequired, output.Failure!.Alert);
        Assert.AreEqual(EchTestConfig.DefaultPublicName, verifier.Presented.Single().HostName);
    }

    [TestMethod]
    public void MalformedRetryConfigsAfterARejectionAreADecodeError()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { EchRetryConfigs = [0, 0] };
        using Tls13ClientHandshake client = Client(EchSettings(EchTestConfig.X25519()));

        Tls13HandshakeOutput output = Run(client, server);

        Assert.AreEqual(TlsAlertDescription.DecodeError, output.Failure!.Alert);
    }

    [TestMethod]
    public void RetryConfigsAfterAnAcceptanceAreAnUnsupportedExtension()
    {
        EchTestConfig config = EchTestConfig.X25519();
        using EchTestFrontEnd frontEnd = new(config);
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { ConfirmEch = true, EchRetryConfigs = config.ConfigList };
        using Tls13ClientHandshake client = Client(EchSettings(config));

        Tls13HandshakeOutput output = Run(client, server, forwardClientHello: frontEnd.Decrypt);

        Assert.AreEqual(TlsAlertDescription.UnsupportedExtension, output.Failure!.Alert);
    }

    [TestMethod]
    public void AnEchOfferDoesNotOfferTheSessionToResume()
    {
        EchTestConfig config = EchTestConfig.X25519();
        using EchTestFrontEnd frontEnd = new(config);
        TlsSessionRecord session = new(0x0304, 0x1301, new byte[32], new byte[32], [1, 2, 3], 7200, 0, 0, DateTimeOffset.UtcNow) { ServerName = "localhost" };
        using Tls13ClientHandshake client = Client(EchSettings(config) with
        {
            ExtensionOrder = [.. OrderWithEch, TlsExtensionType.PskKeyExchangeModes],
            ResumptionSession = session,
        });

        frontEnd.Decrypt(client.Start().BytesToSend[0].Bytes);

        Assert.IsFalse(frontEnd.InnerHellos[0].Extensions.Any(extension => extension.Type == TlsExtensionType.PreSharedKey));
        Assert.IsFalse(frontEnd.OuterHellos[0].Extensions.Any(extension => extension.Type == TlsExtensionType.PreSharedKey));
    }

    [TestMethod]
    public void GreaseEchBytesArePinnedForFixedRandoms()
    {
        byte[] clientRandom = [.. Enumerable.Repeat((byte)0x11, 32)];
        byte[] greasePrivateKey = [.. Enumerable.Repeat((byte)0x22, 32)];
        byte[] payload = [.. Enumerable.Repeat((byte)0x33, GreasePayloadLength)];
        ReplayTlsRandomSource random = new([clientRandom, [0x5a], greasePrivateKey, payload], [new X25519KeyShare([.. Enumerable.Repeat((byte)0x44, 32)])]);
        using Tls13ClientHandshake client = new(GreaseSettings, random, new RecordingCertificateVerifier());

        ClientHello hello = ClientHello.Decode(client.Start().BytesToSend[0].Bytes[HandshakeMessage.HeaderLength..]).Value;

        TlsExtension grease = hello.Extensions[^1];
        Assert.AreEqual(TlsExtensionType.EncryptedClientHello, grease.Type);
        Assert.AreEqual(
            "00" + "0001" + "0001" + "5a" + "0020" + GreaseEncapsulatedKeyHex + GreasePayloadLength.ToString("x4") + Convert.ToHexStringLower(payload),
            Convert.ToHexStringLower(grease.Data));
        Assert.IsFalse(client.EncryptedClientHelloOffered);
        byte[] publicKey = new byte[Cryptography.X25519.KeySize];
        Cryptography.X25519.ComputePublicKey(greasePrivateKey, publicKey);
        Assert.AreEqual(GreaseEncapsulatedKeyHex, Convert.ToHexStringLower(publicKey));
    }

    [TestMethod]
    public void AGreaseHandshakeCompletesAndDropsTheServersRetryConfigs()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { EchRetryConfigs = EchTestConfig.X25519().ConfigList };
        using Tls13ClientHandshake client = Client(GreaseSettings);

        Tls13HandshakeOutput output = Run(client, server);

        Assert.IsTrue(output.IsComplete);
        Assert.IsFalse(client.EncryptedClientHelloAccepted);
        Assert.IsNull(client.EncryptedClientHelloRetryConfigs);
    }

    [TestMethod]
    public void MalformedRetryConfigsAnsweringGreaseAreADecodeError()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { EchRetryConfigs = [0, 1, 0] };
        using Tls13ClientHandshake client = Client(GreaseSettings);

        Tls13HandshakeOutput output = Run(client, server);

        Assert.AreEqual(TlsAlertDescription.DecodeError, output.Failure!.Alert);
    }

    [TestMethod]
    public void TheHelloAfterAHelloRetryRequestRepeatsTheGreaseExtension()
    {
        List<ClientHello> sent = [];
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { Group = TlsNamedGroup.Secp256r1, ConfirmEch = true };
        using Tls13ClientHandshake client = Client(GreaseSettings with { SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1] });

        Tls13HandshakeOutput output = Run(client, server, forwardClientHello: hello =>
        {
            sent.Add(ClientHello.Decode(hello[HandshakeMessage.HeaderLength..]).Value);
            return hello;
        });

        Assert.IsTrue(output.IsComplete);
        Assert.HasCount(2, sent);
        CollectionAssert.AreEqual(EchExtension(sent[0]), EchExtension(sent[1]));
    }

    [TestMethod]
    public void AListWithNoSupportedConfigOffersNothingWithoutGrease()
    {
        EchConfigList unsupported = EchConfigList.Decode(EchTestConfig.EncodeList(EchTestConfig.EncodeConfig(0xfe0d, 1, 0x0011, new byte[97], [1, 1], 0, "p.example"))).Value;
        using Tls13ClientHandshake client = Client(DefaultSettings with { ExtensionOrder = OrderWithEch, EncryptedClientHelloConfigs = unsupported });

        ClientHello hello = ClientHello.Decode(client.Start().BytesToSend[0].Bytes[HandshakeMessage.HeaderLength..]).Value;

        Assert.IsNull(EchExtension(hello));
        Assert.IsFalse(client.EncryptedClientHelloOffered);
    }

    [TestMethod]
    public void AListWithNoSupportedConfigSendsGreaseWhenAsked()
    {
        EchConfigList unsupported = EchConfigList.Decode(EchTestConfig.EncodeList(EchTestConfig.EncodeConfig(0xfe0d, 1, 0x0011, new byte[97], [1, 1], 0, "p.example"))).Value;
        using Tls13ClientHandshake client = Client(GreaseSettings with { EncryptedClientHelloConfigs = unsupported });

        ClientHello hello = ClientHello.Decode(client.Start().BytesToSend[0].Bytes[HandshakeMessage.HeaderLength..]).Value;

        Assert.AreEqual("0000010001", Convert.ToHexStringLower(EchExtension(hello)!.AsSpan(0, 5)));
        Assert.AreEqual("localhost", ServerNameOf(hello));
    }

    [TestMethod]
    public void OfferingEchWithoutItsPlaceInTheOrderIsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { EncryptedClientHelloConfigs = EchTestConfig.X25519().Decoded }));
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { SendEncryptedClientHelloGrease = true }));
    }

    /// <summary>The GREASE payload length for <see cref="GreaseSettings" />: the padded inner hello plus the 16-byte tag.</summary>
    private const int GreasePayloadLength = 176;

    /// <summary>X25519's public key for the private key 0x22 repeated: the GREASE <c>enc</c> is a valid key.</summary>
    private const string GreaseEncapsulatedKeyHex = "0faa684ed28867b97f4a6a2dee5df8ce974e76b7018e3f22a1c4cf2678570f20";

    private static Tls13ClientSettings GreaseSettings => DefaultSettings with { ExtensionOrder = OrderWithEch, SendEncryptedClientHelloGrease = true };

    private static Tls13ClientSettings EchSettings(EchTestConfig config) =>
        DefaultSettings with { ExtensionOrder = OrderWithEch, EncryptedClientHelloConfigs = config.Decoded };

    private static int EncodedInnerLength(Tls13ClientSettings settings)
    {
        using EchTestFrontEnd frontEnd = new(EchTestConfig.X25519(maximumNameLength: settings.EncryptedClientHelloConfigs!.Configs[0].MaximumNameLength));
        using Tls13ClientHandshake client = Client(settings);
        frontEnd.Decrypt(client.Start().BytesToSend[0].Bytes);
        return frontEnd.EncodedInnerLengths[0];
    }

    private static IReadOnlyList<ushort> OfferedVersions(ClientHello hello)
    {
        TlsReader reader = new(hello.Extensions.Single(extension => extension.Type == TlsExtensionType.SupportedVersions).Data);
        return reader.ReadUInt16List(1);
    }

    private static byte[]? EchExtension(ClientHello hello) =>
        hello.Extensions.FirstOrDefault(extension => extension.Type == TlsExtensionType.EncryptedClientHello)?.Data;

    private static byte[] FlipLastRandomByte(byte[] serverHello)
    {
        byte[] flipped = [.. serverHello];
        flipped[HandshakeMessage.HeaderLength + 2 + 31] ^= 0x01;
        return flipped;
    }

    private static byte[] ReplaceEchExtension(byte[] retry, byte[] data)
    {
        ServerHello decoded = ServerHello.Decode(retry[HandshakeMessage.HeaderLength..]).Value;
        return (decoded with
        {
            Extensions = [.. decoded.Extensions.Select(extension => extension.Type == TlsExtensionType.EncryptedClientHello ? new TlsExtension(extension.Type, data) : extension)],
        }).Encode();
    }
}
