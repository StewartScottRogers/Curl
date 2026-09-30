using Curl.Cryptography;

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

    [TestMethod]
    public void DecodeReadsEveryFieldOfAConfig()
    {
        TlsExtension extension = new((TlsExtensionType)0x1234, [9]);
        byte[] config = EchTestConfig.EncodeConfig(0xfe0d, 0x2a, 0x0020, X25519PublicKey, [0x0001, 0x0003, 0x0001, 0x0001], 64, "public.example", [extension]);

        TlsDecodeResult<EchConfigList> decoded = EchConfigList.Decode(EchTestConfig.EncodeList(config));

        EchConfig read = decoded.Value.Configs.Single();
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

        EchConfigList list = EchConfigList.Decode(EchTestConfig.EncodeList(other, current)).Value;

        Assert.AreEqual(EchTestConfig.DefaultPublicName, list.Configs.Single().PublicName);
        Assert.AreSame(list.Configs[0], list.SupportedConfig);
    }

    [TestMethod]
    public void AListOfOtherVersionsAloneHasNoSupportedConfig()
    {
        byte[] other = EchTestConfig.EncodeConfig(0xfe0c, 1, 0x0020, X25519PublicKey, [1, 1], 0, "old.example");

        EchConfigList list = EchConfigList.Decode(EchTestConfig.EncodeList(other)).Value;

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
        TlsDecodeResult<EchConfigList> decoded = EchConfigList.Decode(Convert.FromHexString(hex));

        Assert.AreEqual(TlsAlertDescription.DecodeError, decoded.Alert);
    }

    [TestMethod]
    public void AMalformedConfigOfAnotherVersionIsSkippedUnread()
    {
        EchConfigList list = EchConfigList.Decode(Convert.FromHexString("0006fe0c0002ffff")).Value;

        Assert.IsEmpty(list.Configs);
    }

    [TestMethod]
    [DataRow(0x0011, DisplayName = "DHKEM(P-384) is not supported")]
    [DataRow(0x0021, DisplayName = "DHKEM(X448) is not supported")]
    public void AConfigWithAKemTheClientLacksIsNotSupported(int kemId)
    {
        EchConfig config = Decode(EchTestConfig.EncodeConfig(0xfe0d, 1, (ushort)kemId, X25519PublicKey, [1, 1], 0, "p.example"));

        Assert.IsNull(config.FindSupportedSuite());
    }

    [TestMethod]
    public void AConfigWhoseSuitesTheClientLacksIsNotSupported()
    {
        EchConfig config = Decode(EchTestConfig.EncodeConfig(0xfe0d, 1, 0x0020, X25519PublicKey, [2, 1, 1, 0xffff], 0, "p.example"));

        Assert.IsNull(config.FindSupportedSuite());
    }

    [TestMethod]
    public void AConfigWithAMandatoryExtensionIsNotSupported()
    {
        EchConfig config = Decode(EchTestConfig.EncodeConfig(0xfe0d, 1, 0x0020, X25519PublicKey, [1, 1], 0, "p.example", [new TlsExtension((TlsExtensionType)0x8001, [])]));

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
        EchConfig config = Decode(EchTestConfig.EncodeConfig(0xfe0d, 1, (ushort)kemId, publicKey, [1, 1], 0, "p.example"));

        Assert.IsNull(config.FindSupportedSuite());
    }

    [TestMethod]
    public void TheSupportedConfigIsTheFirstTheClientCanUse()
    {
        byte[] unsupported = EchTestConfig.EncodeConfig(0xfe0d, 1, 0x0011, X25519PublicKey, [1, 1], 0, "first.example");
        byte[] supported = EchTestConfig.EncodeConfig(0xfe0d, 2, 0x0020, X25519PublicKey, [1, 2], 0, "second.example");

        EchConfigList list = EchConfigList.Decode(EchTestConfig.EncodeList(unsupported, supported)).Value;

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
        Assert.AreEqual(supported, new EchCipherSuite((ushort)kdfId, (ushort)aeadId).IsSupported);
    }

    [TestMethod]
    public void DecodeRefusesNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EchConfigList.Decode(null!));
    }

    [TestMethod]
    public void TheExtensionCodecsWriteAndReadEachShape()
    {
        TlsExtension outer = EncryptedClientHelloExtension.EncodeOuter(new EchCipherSuite(1, 3), 0x2a, [0xee], [0xdd, 0xcc]);

        Assert.AreEqual("0000010003" + "2a" + "0001ee" + "0002ddcc", Convert.ToHexStringLower(outer.Data));
        CollectionAssert.AreEqual(new byte[] { 1 }, EncryptedClientHelloExtension.EncodeInner().Data);
        byte[] list = EchTestConfig.X25519().ConfigList;
        CollectionAssert.AreEqual(list, EncryptedClientHelloExtension.EncodeRetryConfigs(list).Data);
        Assert.AreEqual(EchTestConfig.DefaultPublicName, EncryptedClientHelloExtension.DecodeRetryConfigs(list).Value.SupportedConfig!.PublicName);
        CollectionAssert.AreEqual(new byte[8], EncryptedClientHelloExtension.DecodeRetryConfirmation(new byte[8]).Value);
        Assert.AreEqual(TlsAlertDescription.DecodeError, EncryptedClientHelloExtension.DecodeRetryConfirmation(new byte[7]).Alert);
        Assert.AreEqual(TlsAlertDescription.DecodeError, EncryptedClientHelloExtension.DecodeRetryConfirmation(new byte[9]).Alert);
    }

    [TestMethod]
    public void TheGreaseSuiteIsHkdfSha256WithAes128Gcm()
    {
        Assert.AreEqual(new EchCipherSuite((ushort)HpkeKdf.HkdfSha256, (ushort)HpkeAead.Aes128Gcm), EchCipherSuite.Grease);
    }

    private static EchConfig Decode(byte[] config) => EchConfigList.Decode(EchTestConfig.EncodeList(config)).Value.Configs.Single();
}
