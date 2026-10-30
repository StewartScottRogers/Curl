using Curl.Cryptography;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

[TestClass]
public sealed class KeyConfigurationTests
{
    [TestMethod]
    public void Parse_Rfc9458Example_GivesItsKeyIdKemPublicKeyAndSuites()
    {
        KeyConfiguration configuration = KeyConfiguration.Parse(Convert.FromHexString(Rfc9458Example.KeyConfiguration));

        Assert.AreEqual(1, configuration.KeyId);
        Assert.AreEqual(HpkeKem.DhkemX25519HkdfSha256, configuration.Kem);
        Assert.AreEqual(Rfc9458Example.PublicKey, Convert.ToHexStringLower(configuration.PublicKey));
        CollectionAssert.AreEqual(
            new[] { new SymmetricSuite(HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm), new SymmetricSuite(HpkeKdf.HkdfSha256, HpkeAead.ChaCha20Poly1305) },
            configuration.SymmetricSuites.ToArray());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("0100")]
    [DataRow("010020")]
    [DataRow("010020" + Rfc9458Example.PublicKey + "00")]
    [DataRow("010020" + Rfc9458Example.PublicKey + "000800010001")]
    [DataRow("010020" + Rfc9458Example.PublicKey + "0004000100010001")]
    [DataRow("010020" + Rfc9458Example.PublicKey + "0000")]
    [DataRow("010020" + Rfc9458Example.PublicKey + "00020001")]
    public void Parse_TruncatedOrRagged_ThrowsMalformedKeyConfigurationException(string configuration)
    {
        Assert.ThrowsExactly<MalformedKeyConfigurationException>(() => KeyConfiguration.Parse(Convert.FromHexString(configuration)));
    }

    [TestMethod]
    public void Parse_UnknownKem_ThrowsUnsupportedKeyConfigurationExceptionNamingIt()
    {
        UnsupportedKeyConfigurationException exception = Assert.ThrowsExactly<UnsupportedKeyConfigurationException>(
            () => KeyConfiguration.Parse(Convert.FromHexString("010021" + Rfc9458Example.PublicKey + "000400010001")));

        StringAssert.Contains(exception.Message, "0x0021");
    }

    [TestMethod]
    public void Parse_P256Kem_DelimitsA65BytePublicKey()
    {
        string publicKey = "04" + new string('a', 128);

        KeyConfiguration configuration = KeyConfiguration.Parse(Convert.FromHexString("070010" + publicKey + "000400010002"));

        Assert.AreEqual(HpkeKem.DhkemP256HkdfSha256, configuration.Kem);
        Assert.AreEqual(publicKey, Convert.ToHexStringLower(configuration.PublicKey));
    }
}
