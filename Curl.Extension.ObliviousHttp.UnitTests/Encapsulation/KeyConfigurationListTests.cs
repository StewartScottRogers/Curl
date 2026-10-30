using Curl.Cryptography;
using Curl.Extension.ObliviousHttp.Hpke;

namespace Curl.Extension.ObliviousHttp.Encapsulation;

[TestClass]
public sealed class KeyConfigurationListTests
{
    private const string ExampleEntry = "002d" + Rfc9458Example.KeyConfiguration;

    private const string UnsupportedSuitesEntry = "002d020020" + Rfc9458Example.PublicKey + "00080002000100010004";

    private const string UnknownKemEntry = "0029030021" + Rfc9458Example.PublicKey + "000400010001";

    [TestMethod]
    public void Parse_ListHoldingTheRfc9458Example_ParsesItTheSame()
    {
        KeyConfiguration configuration = KeyConfigurationList.Parse(Convert.FromHexString(ExampleEntry)).Single();

        Assert.AreEqual(1, configuration.KeyId);
        Assert.AreEqual(HpkeKem.DhkemX25519HkdfSha256, configuration.Kem);
        Assert.AreEqual(Rfc9458Example.PublicKey, Convert.ToHexStringLower(configuration.PublicKey));
        Assert.HasCount(2, configuration.SymmetricSuites);
    }

    [TestMethod]
    public void Parse_Empty_GivesNoConfigurations()
    {
        Assert.IsEmpty(KeyConfigurationList.Parse([]));
    }

    [TestMethod]
    public void Parse_UnknownKemEntry_LeavesItOutAndKeepsTheRest()
    {
        IReadOnlyList<KeyConfiguration> configurations = KeyConfigurationList.Parse(Convert.FromHexString(UnknownKemEntry + UnsupportedSuitesEntry + ExampleEntry));

        CollectionAssert.AreEqual(new byte[] { 2, 1 }, configurations.Select(configuration => configuration.KeyId).ToArray());
    }

    [TestMethod]
    [DataRow("00")]
    [DataRow("002e" + Rfc9458Example.KeyConfiguration)]
    [DataRow(ExampleEntry + "00")]
    [DataRow("0000")]
    public void Parse_TruncatedList_ThrowsMalformedKeyConfigurationException(string list)
    {
        Assert.ThrowsExactly<MalformedKeyConfigurationException>(() => KeyConfigurationList.Parse(Convert.FromHexString(list)));
    }

    [TestMethod]
    public void SelectFirstSupported_SkipsConfigurationsWithoutASupportedSuite_ChoosesTheFirstSupportedSuite()
    {
        KeyConfigurationChoice choice = KeyConfigurationList.SelectFirstSupported(KeyConfigurationList.Parse(Convert.FromHexString(UnsupportedSuitesEntry + ExampleEntry)));

        Assert.AreEqual(1, choice.Configuration.KeyId);
        Assert.AreEqual(new HpkeSuite(HpkeKem.DhkemX25519HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.Aes128Gcm), choice.Suite);
    }

    [TestMethod]
    public void SelectFirstSupported_SkipsAnUnsupportedSuiteWithinAConfiguration()
    {
        KeyConfiguration configuration = KeyConfiguration.Parse(Convert.FromHexString("040020" + Rfc9458Example.PublicKey + "00080002000100010003"));

        Assert.AreEqual(HpkeAead.ChaCha20Poly1305, KeyConfigurationList.SelectFirstSupported([configuration]).Suite.Aead);
    }

    [TestMethod]
    public void SelectFirstSupported_NoSupportedSuite_ThrowsUnsupportedKeyConfigurationException()
    {
        IReadOnlyList<KeyConfiguration> configurations = KeyConfigurationList.Parse(Convert.FromHexString(UnsupportedSuitesEntry));

        Assert.ThrowsExactly<UnsupportedKeyConfigurationException>(() => KeyConfigurationList.SelectFirstSupported(configurations));
    }

    [TestMethod]
    public void SelectFirstSupported_NoConfigurations_ThrowsUnsupportedKeyConfigurationException()
    {
        Assert.ThrowsExactly<UnsupportedKeyConfigurationException>(() => KeyConfigurationList.SelectFirstSupported([]));
    }
}
