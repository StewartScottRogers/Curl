using Curl.Cryptography;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Decodes <c>ECHConfigList</c>s (RFC 9849 section 4): every field of a config, configs of
/// another version skipped, every malformed shape a typed <c>decode_error</c>, and the choice
/// of the config and suite the client offers.
/// </summary>
[TestClass]
public sealed class EchConfigListTests
{
    private static readonly byte[] X25519PublicKey = EchTestConfig.X25519().PublicKey;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void DecodeReadsEveryFieldOfAConfig()
    {
        TlsExtension extension = new((TlsExtensionType)0x1234, [9]);
        byte[] config = EchTestConfig.EncodeConfig(0xfe0d, 0x2a, 0x0020, X25519PublicKey, [0x0001, 0x0003, 0x0001, 0x0001], 64, "public.example", [extension]);
        Diagnostics.Bytes("config", config);
        Diagnostics.Arrange("config", "version 0xfe0d, id 0x2a, KEM 0x0020, suites (1,3) (1,1), maximum name length 64, public.example, extension 0x1234");

        TlsDecodeResult<EchConfigList> decoded = EchConfigList.Decode(EchTestConfig.EncodeList(config));

        EchConfig read = decoded.Value.Configs.Single();
        WriteConfig(read);
        Diagnostics.Assert("public name", "public.example", read.PublicName);
        Diagnostics.Assert("supported suite", new EchCipherSuite(1, 3), read.FindSupportedSuite());
        Diagnostics.Diff("re-encoded config", config, read.Encoded);
        Assert.AreEqual(0x2a, read.ConfigId);
        Assert.AreEqual(0x0020, read.KemId);
        CollectionAssert.AreEqual(X25519PublicKey, read.PublicKey);
        CollectionAssert.AreEqual(new[] { new EchCipherSuite(1, 3), new EchCipherSuite(1, 1) }, read.CipherSuites.ToArray());
        Assert.AreEqual(64, read.MaximumNameLength);
        Assert.AreEqual("public.example", read.PublicName);
        Assert.AreEqual((TlsExtensionType)0x1234, read.Extensions.Single().Type);
        CollectionAssert.AreEqual(config, read.Encoded);
        CollectionAssert.AreEqual(EchTestConfig.EncodeList(config), decoded.Value.Encoded);
        Assert.AreEqual(new EchCipherSuite(1, 3), read.FindSupportedSuite());
    }

    [TestMethod]
    public void DecodeSkipsAConfigOfAnotherVersion()
    {
        byte[] other = EchTestConfig.EncodeConfig(0xfe0c, 1, 0x0020, X25519PublicKey, [1, 1], 0, "old.example");
        byte[] current = EchTestConfig.X25519().Encode();
        Diagnostics.Arrange("configs", $"version 0xfe0c old.example, then version 0xfe0d {EchTestConfig.DefaultPublicName}");

        EchConfigList list = EchConfigList.Decode(EchTestConfig.EncodeList(other, current)).Value;

        WriteList(list);
        Diagnostics.Assert("public name", EchTestConfig.DefaultPublicName, list.Configs.Single().PublicName);
        Assert.AreEqual(EchTestConfig.DefaultPublicName, list.Configs.Single().PublicName);
        Assert.AreSame(list.Configs[0], list.SupportedConfig);
    }

    [TestMethod]
    public void AListOfOtherVersionsAloneHasNoSupportedConfig()
    {
        byte[] other = EchTestConfig.EncodeConfig(0xfe0c, 1, 0x0020, X25519PublicKey, [1, 1], 0, "old.example");
        Diagnostics.Arrange("configs", "version 0xfe0c old.example only");

        EchConfigList list = EchConfigList.Decode(EchTestConfig.EncodeList(other)).Value;

        WriteList(list);
        Diagnostics.Assert("configs read", 0, list.Configs.Count);
        Assert.IsEmpty(list.Configs);
        Assert.IsNull(list.SupportedConfig);
    }

    [TestMethod]
    [DataRow("0000", DisplayName = "An empty list")]
    [DataRow("00", DisplayName = "A list length cut short")]
    [DataRow("0005fe0d0001", DisplayName = "A list length past the end")]
    [DataRow("0004fe0c0000" + "00", DisplayName = "Bytes after the list")]
    [DataRow("0004fe0d0000", DisplayName = "An empty config")]
    [DataRow("0014fe0d0010" + "2a" + "0020" + "0000" + "0004" + "00010001" + "00" + "0161" + "0000", DisplayName = "An empty public key")]
    [DataRow("0014fe0d0010" + "2a" + "0020" + "000101" + "0004" + "00010001" + "00" + "00" + "0000", DisplayName = "An empty public name")]
    [DataRow("0011fe0d000d" + "2a" + "0020" + "000101" + "0000" + "00" + "0161" + "0000", DisplayName = "An empty suite list")]
    [DataRow("0017fe0d0013" + "2a" + "0020" + "000101" + "0006" + "000100010001" + "00" + "0161" + "0000", DisplayName = "A suite list of three identifiers")]
    [DataRow("0016fe0d0012" + "2a" + "0020" + "000101" + "0005" + "0001000100" + "00" + "0161" + "0000", DisplayName = "A suite list of odd length")]
    [DataRow("0016fe0d0012" + "2a" + "0020" + "000101" + "0004" + "00010001" + "00" + "0161" + "000100", DisplayName = "An extension list cut short")]
    public void AMalformedListIsADecodeError(string hex)
    {
        Diagnostics.Arrange("list", hex);

        TlsDecodeResult<EchConfigList> decoded = EchConfigList.Decode(Convert.FromHexString(hex));

        Diagnostics.Act("alert", Describe(decoded.Alert));
        Diagnostics.Assert("alert", Describe(TlsAlertDescription.DecodeError), Describe(decoded.Alert));
        Assert.AreEqual(TlsAlertDescription.DecodeError, decoded.Alert);
    }

    [TestMethod]
    public void AMalformedConfigOfAnotherVersionIsSkippedUnread()
    {
        Diagnostics.Arrange("list", "0006fe0c0002ffff");

        EchConfigList list = EchConfigList.Decode(Convert.FromHexString("0006fe0c0002ffff")).Value;

        WriteList(list);
        Diagnostics.Assert("configs read", 0, list.Configs.Count);
        Assert.IsEmpty(list.Configs);
    }

    [TestMethod]
    [DataRow(0x0011, DisplayName = "DHKEM(P-384) is not supported")]
    [DataRow(0x0021, DisplayName = "DHKEM(X448) is not supported")]
    public void AConfigWithAKemTheClientLacksIsNotSupported(int kemId)
    {
        Diagnostics.Arrange("KEM", $"0x{kemId:x4}");
        EchConfig config = Decode(EchTestConfig.EncodeConfig(0xfe0d, 1, (ushort)kemId, X25519PublicKey, [1, 1], 0, "p.example"));

        AssertNoSupportedSuite(config.FindSupportedSuite());
        Assert.IsNull(config.FindSupportedSuite());
    }

    [TestMethod]
    public void AConfigWhoseSuitesTheClientLacksIsNotSupported()
    {
        Diagnostics.Arrange("suites", "(2,1) (1,0xffff)");
        EchConfig config = Decode(EchTestConfig.EncodeConfig(0xfe0d, 1, 0x0020, X25519PublicKey, [2, 1, 1, 0xffff], 0, "p.example"));

        AssertNoSupportedSuite(config.FindSupportedSuite());
        Assert.IsNull(config.FindSupportedSuite());
    }

    [TestMethod]
    public void AConfigWithAMandatoryExtensionIsNotSupported()
    {
        Diagnostics.Arrange("extension", "0x8001, mandatory");
        EchConfig config = Decode(EchTestConfig.EncodeConfig(0xfe0d, 1, 0x0020, X25519PublicKey, [1, 1], 0, "p.example", [new TlsExtension((TlsExtensionType)0x8001, [])]));

        AssertNoSupportedSuite(config.FindSupportedSuite());
        Assert.IsNull(config.FindSupportedSuite());
    }

    [TestMethod]
    [DataRow(0x0020, 32, DisplayName = "An all-zero X25519 key is of small order")]
    [DataRow(0x0020, 31, DisplayName = "An X25519 key a byte short")]
    [DataRow(0x0010, 65, DisplayName = "A P-256 point that is not on the curve")]
    public void AConfigWithAnUnusablePublicKeyIsNotSupported(int kemId, int keyLength)
    {
        byte[] publicKey = new byte[keyLength];
        publicKey[0] = kemId == 0x0010 ? (byte)0x04 : (byte)0;
        Diagnostics.Arrange("KEM", $"0x{kemId:x4}");
        Diagnostics.Bytes("public key", publicKey);
        EchConfig config = Decode(EchTestConfig.EncodeConfig(0xfe0d, 1, (ushort)kemId, publicKey, [1, 1], 0, "p.example"));

        AssertNoSupportedSuite(config.FindSupportedSuite());
        Assert.IsNull(config.FindSupportedSuite());
    }

    [TestMethod]
    public void TheSupportedConfigIsTheFirstTheClientCanUse()
    {
        byte[] unsupported = EchTestConfig.EncodeConfig(0xfe0d, 1, 0x0011, X25519PublicKey, [1, 1], 0, "first.example");
        byte[] supported = EchTestConfig.EncodeConfig(0xfe0d, 2, 0x0020, X25519PublicKey, [1, 2], 0, "second.example");
        Diagnostics.Arrange("configs", "first.example with KEM 0x0011, then second.example with KEM 0x0020 and suite (1,2)");

        EchConfigList list = EchConfigList.Decode(EchTestConfig.EncodeList(unsupported, supported)).Value;

        WriteList(list);
        Diagnostics.Assert("supported config", "second.example", list.SupportedConfig?.PublicName);
        Assert.AreEqual("second.example", list.SupportedConfig!.PublicName);
        Assert.AreEqual(new EchCipherSuite(1, 2), list.SupportedConfig.FindSupportedSuite());
    }

    [TestMethod]
    [DataRow(1, 1, true)]
    [DataRow(1, 2, true)]
    [DataRow(1, 3, true)]
    [DataRow(1, 0xffff, false)]
    [DataRow(2, 1, false)]
    public void ACipherSuiteIsSupportedWithHkdfSha256AndAnAeadHpkeSeals(int kdfId, int aeadId, bool supported)
    {
        Diagnostics.Arrange("suite", $"KDF 0x{kdfId:x4}, AEAD 0x{aeadId:x4}");

        bool isSupported = new EchCipherSuite((ushort)kdfId, (ushort)aeadId).IsSupported;

        Diagnostics.Act("supported", isSupported);
        Diagnostics.Assert("supported", supported, isSupported);
        Assert.AreEqual(supported, isSupported);
    }

    [TestMethod]
    public void DecodeRefusesNull()
    {
        Diagnostics.Arrange("list", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => EchConfigList.Decode(null!));

        Diagnostics.Act("exception parameter", exception.ParamName);
        Diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void TheExtensionCodecsWriteAndReadEachShape()
    {
        Diagnostics.Arrange("outer", "suite (1,3), config id 0x2a, enc ee, payload ddcc");

        TlsExtension outer = EncryptedClientHelloExtension.EncodeOuter(new EchCipherSuite(1, 3), 0x2a, [0xee], [0xdd, 0xcc]);

        Diagnostics.Act("outer", Convert.ToHexStringLower(outer.Data));
        Diagnostics.Diff("outer", "0000010003" + "2a" + "0001ee" + "0002ddcc", Convert.ToHexStringLower(outer.Data));
        Assert.AreEqual("0000010003" + "2a" + "0001ee" + "0002ddcc", Convert.ToHexStringLower(outer.Data));
        CollectionAssert.AreEqual(new byte[] { 1 }, EncryptedClientHelloExtension.EncodeInner().Data);
        byte[] list = EchTestConfig.X25519().ConfigList;
        Diagnostics.Bytes("retry configs", list);
        CollectionAssert.AreEqual(list, EncryptedClientHelloExtension.EncodeRetryConfigs(list).Data);
        Assert.AreEqual(EchTestConfig.DefaultPublicName, EncryptedClientHelloExtension.DecodeRetryConfigs(list).Value.SupportedConfig!.PublicName);
        CollectionAssert.AreEqual(new byte[8], EncryptedClientHelloExtension.DecodeRetryConfirmation(new byte[8]).Value);
        Diagnostics.Assert("7-byte confirmation alert", Describe(TlsAlertDescription.DecodeError), Describe(EncryptedClientHelloExtension.DecodeRetryConfirmation(new byte[7]).Alert));
        Diagnostics.Assert("9-byte confirmation alert", Describe(TlsAlertDescription.DecodeError), Describe(EncryptedClientHelloExtension.DecodeRetryConfirmation(new byte[9]).Alert));
        Assert.AreEqual(TlsAlertDescription.DecodeError, EncryptedClientHelloExtension.DecodeRetryConfirmation(new byte[7]).Alert);
        Assert.AreEqual(TlsAlertDescription.DecodeError, EncryptedClientHelloExtension.DecodeRetryConfirmation(new byte[9]).Alert);
    }

    [TestMethod]
    public void TheGreaseSuiteIsHkdfSha256WithAes128Gcm()
    {
        Diagnostics.Arrange("expected", $"KDF {HpkeKdf.HkdfSha256}, AEAD {HpkeAead.Aes128Gcm}");

        EchCipherSuite grease = EchCipherSuite.Grease;

        Diagnostics.Act("grease suite", grease);
        Diagnostics.Assert("grease suite", new EchCipherSuite((ushort)HpkeKdf.HkdfSha256, (ushort)HpkeAead.Aes128Gcm), grease);
        Assert.AreEqual(new EchCipherSuite((ushort)HpkeKdf.HkdfSha256, (ushort)HpkeAead.Aes128Gcm), EchCipherSuite.Grease);
    }

    private static EchConfig Decode(byte[] config) => EchConfigList.Decode(EchTestConfig.EncodeList(config)).Value.Configs.Single();

    private static string Describe(TlsAlertDescription? alert) =>
        alert is { } description ? $"{description} ({(byte)description})" : "none";

    private void WriteConfig(EchConfig config)
    {
        Diagnostics.Act("config", $"id 0x{config.ConfigId:x2}, KEM 0x{config.KemId:x4}, suites {string.Join(" ", config.CipherSuites)}, maximum name length {config.MaximumNameLength}, public name {config.PublicName}");
        Diagnostics.Bytes("public key", config.PublicKey);
    }

    private void WriteList(EchConfigList list)
    {
        Diagnostics.Act("configs read", list.Configs.Count);
        Diagnostics.Act("supported config", list.SupportedConfig?.PublicName ?? "none");
    }

    private void AssertNoSupportedSuite(EchCipherSuite? suite)
    {
        Diagnostics.Act("supported suite", suite?.ToString() ?? "none");
        Diagnostics.Assert("supported suite", "none", suite?.ToString() ?? "none");
    }
}
