namespace Curl.Tls;

/// <summary>
/// Rebuilds each of ADR-0140's three captured ClientHellos from its profile, byte for byte,
/// with the random, legacy session ID and key shares taken from the capture. The OpenSSL
/// capture elides its 1216-byte X25519MLKEM768 share, so a test value of that length
/// stands in for it on both sides.
/// </summary>
[TestClass]
public sealed class ClientHelloProfileTests
{
    private const string LibreSslCapture =
        "16030301300100012c0303d6b8f746101d9144e53df3ad5bd3e73b5c51a39dbc499ec8ad049ed692b969b6201bdd3f3893799988f6e86bd9d164df715c927e4eda868e83c83d81536e545884005c130213031301c030c02cc028c024c014c00a009f006b0039cca9cca8ccaa00c40088009d003d003500c00084c02fc02bc027c023c013c009009e0067003300be0045009c003c002f00ba0041c011c0070005c012c0080016000a00ff01000087000d00180016080606010603080505010503080404010403020102030000000e000c0000096c6f63616c686f7374000b00020100003300260024001d0020399ce519cb9d0ffab9d3ded478a27cf46e3d2cdf9ff34de0f80d685c2855e80c002b00050403040303000a000a0008001d0017001800190010000e000c02683208687474702f312e31";

    private const string SchannelCapture =
        "16030101c3010001bf0303b87008b2784fa83d52bb21596b41f88b0bc30b23dce3b50d780434224c4be5fe2030f1b9a25b28972dbe1624088642528acddfbb1e0a9801818be41bb3cee1df4c002813021301c02cc02bc030c02fc024c023c028c027c00ac009c014c013009d009c003d003c0035002f0100014e0000000e000c0000096c6f63616c686f7374000500050100000000002b00050403040303000d001a001808040805080604010501020104030503020302020601060300230000000a00080006001d00170018000b000201000010000b000908687474702f312e31003300d000ce001d00202b0f62427470b052fd7516bf732d72fc0d5438b0e6ced503e6530a375d7d512c0017004104964a6a7f68e3c191788a434128cdc8a2b4ae0ef52149d9d7eeaca045b2415c9cd5249b7f41607f83192bc563b51eeb2e3f4f75bdf9553b33ecd3efd6da47484800180061041d0faf506f70d0093645aa03a28006f27a2b61c3eb57a92581cbf4eeeaff5618dae57bb0b80b087f32008ae58a5e909a86b792c326000dcba4dc279e7c00a87c5433ce8cda35c10fdf77d120022e75fcf7a1e1eed6d437c2faefeda50d0524be0031000000170000ff01000100002d00020101";

    private const string OpenSslCaptureBeforeMlKemShare =
        "160301061c010006180303144ae9da9aee4e9aa064b7b6bb8d449e9ca68bd43b5ac3b8ae044536fdb6999d20fe793b4558d3ea23de61ca238d28b1431874c2c5c19432603514be5d62364c27003c130213031301c02cc030009fcca9cca8ccaac02bc02f009ec024c028006bc023c0270067c00ac0140039c009c0130033009d009c003d003c0035002f01000593ff010001000000000e000c0000096c6f63616c686f7374000b000403000102000a0012001011ec001d0017001e00180019010001010010000e000c02683208687474702f312e31001600000017000000310000000d0036003409050906090404030503060308070808081a081b081c0809080a080b080408050806040105010601030303010302040205020602002b00050403040303002d00020101003304ea04e811ec04c0";

    private const string OpenSslCaptureAfterMlKemShare =
        "001d002072e3a456c8f1d674c88d3b3b6decb2175a3217e875f685544aaa0ee4cc2de209001b00050400010003";

    private const int X25519MlKem768ShareLength = 1216;

    [TestMethod]
    public void LibreSslProfileRebuildsTheCapturedHello()
    {
        AssertProfileRebuilds(ClientHelloProfile.LibreSsl, Convert.FromHexString(LibreSslCapture));
    }

    [TestMethod]
    public void OnlySchannelsTls12HelloIsInARecordOfItsCeiling()
    {
        Assert.IsTrue(ClientHelloProfile.Schannel.Tls12RecordVersionIsTheCeiling);
        Assert.IsFalse(ClientHelloProfile.OpenSsl.Tls12RecordVersionIsTheCeiling);
        Assert.IsFalse(ClientHelloProfile.LibreSsl.Tls12RecordVersionIsTheCeiling);
    }

    [TestMethod]
    public void SchannelProfileRebuildsTheCapturedHello()
    {
        AssertProfileRebuilds(ClientHelloProfile.Schannel, Convert.FromHexString(SchannelCapture));
    }

    [TestMethod]
    public void OpenSslProfileRebuildsTheCapturedHelloWithATestMlKemShare()
    {
        byte[] mlKemShare = [.. Enumerable.Range(0, X25519MlKem768ShareLength).Select(index => (byte)index)];
        byte[] capture = Convert.FromHexString(OpenSslCaptureBeforeMlKemShare + Convert.ToHexString(mlKemShare) + OpenSslCaptureAfterMlKemShare);

        Assert.HasCount(1569, capture);
        AssertProfileRebuilds(ClientHelloProfile.OpenSsl, capture);
    }

    [TestMethod]
    public void OpenSslProfileOffersCertificateCompressionAsCaptured()
    {
        // The capture ends with compress_certificate (27): zlib and zstd, no brotli (ADR-0199).
        const string CapturedCompressCertificate = "001b00050400010003";
        TlsExtension extension = CompressCertificateExtension.Encode(ClientHelloProfile.OpenSsl.CertificateCompressionAlgorithms);

        CollectionAssert.AreEqual(new ushort[] { CertificateCompressionAlgorithm.Zlib, CertificateCompressionAlgorithm.Zstd }, ClientHelloProfile.OpenSsl.CertificateCompressionAlgorithms.ToArray());
        Assert.EndsWith(CapturedCompressCertificate, OpenSslCaptureAfterMlKemShare);
        Assert.AreEqual(CapturedCompressCertificate, "001b0005" + Convert.ToHexString(extension.Data).ToLowerInvariant());
    }

    [TestMethod]
    public void ProfilesShareTheGroupsTheCapturesShare()
    {
        CollectionAssert.AreEqual(new ushort[] { 0x001d }, ClientHelloProfile.LibreSsl.KeyShareGroups.ToArray());
        CollectionAssert.AreEqual(new ushort[] { 0x001d, 0x0017, 0x0018 }, ClientHelloProfile.Schannel.KeyShareGroups.ToArray());
        CollectionAssert.AreEqual(new ushort[] { 0x11ec, 0x001d }, ClientHelloProfile.OpenSsl.KeyShareGroups.ToArray());
    }

    [TestMethod]
    public void AChangedListKeepsTheExtensionOrder()
    {
        ClientHelloProfile profile = ClientHelloProfile.Schannel with { SupportedGroups = [0x0017] };

        ClientHello hello = profile.Build("a", new byte[ClientHello.RandomLength], [], []);

        CollectionAssert.AreEqual(ClientHelloProfile.Schannel.ExtensionOrder.ToArray(), hello.Extensions.Select(extension => extension.Type).ToArray());
        TlsExtension groups = hello.Extensions.Single(extension => extension.Type == TlsExtensionType.SupportedGroups);
        CollectionAssert.AreEqual(new ushort[] { 0x0017 }, SupportedGroupsExtension.Decode(groups.Data).Value.ToArray());
    }

    [TestMethod]
    public void AnExtensionNoProfileBuildsIsRefused()
    {
        ClientHelloProfile profile = ClientHelloProfile.LibreSsl with { ExtensionOrder = [TlsExtensionType.Cookie] };

        Assert.ThrowsExactly<InvalidOperationException>(() => profile.Build("a", new byte[ClientHello.RandomLength], [], []));
    }

    [TestMethod]
    public void EncodeRecordRefusesANullHello()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ClientHelloProfile.OpenSsl.EncodeRecord(null!));
    }

    private static void AssertProfileRebuilds(ClientHelloProfile profile, byte[] capture)
    {
        ClientHello captured = ClientHello.Decode(capture[9..]).Value;
        TlsExtension keyShare = captured.Extensions.Single(extension => extension.Type == TlsExtensionType.KeyShare);
        IReadOnlyList<KeyShareEntry> keyShares = KeyShareExtension.DecodeClientShares(keyShare.Data).Value;

        ClientHello rebuilt = profile.Build("localhost", captured.Random, captured.LegacySessionId, keyShares);

        CollectionAssert.AreEqual(profile.KeyShareGroups.ToArray(), keyShares.Select(share => share.Group).ToArray());
        Assert.AreEqual(Convert.ToHexStringLower(capture), Convert.ToHexStringLower(profile.EncodeRecord(rebuilt)));
    }
}
