namespace Curl.Kerberos;

/// <summary>
/// Checks <see cref="KerberosAsn1.WithoutPadding" />, which drops the zero padding
/// <c>des3-cbc-sha1</c> leaves after a decrypted value, as MIT's <c>k5_asn1_full_decode</c> ignores it.
/// </summary>
[TestClass]
public sealed class KerberosAsn1Tests
{
    [TestMethod]
    public void WithoutPadding_ValueThenZeros_GivesTheValueAndZeroesThePlaintext()
    {
        byte[] plaintext = [0x30, 0x03, 0x02, 0x01, 0x05, 0x00, 0x00, 0x00];

        byte[] value = KerberosAsn1.WithoutPadding(plaintext);

        CollectionAssert.AreEqual(new byte[] { 0x30, 0x03, 0x02, 0x01, 0x05 }, value);
        Assert.IsTrue(plaintext.All(octet => octet == 0));
    }

    [TestMethod]
    public void WithoutPadding_ExactlyOneValue_GivesThePlaintextItself()
    {
        byte[] plaintext = [0x30, 0x03, 0x02, 0x01, 0x05];

        Assert.AreSame(plaintext, KerberosAsn1.WithoutPadding(plaintext));
    }

    [TestMethod]
    public void WithoutPadding_NotAValue_GivesThePlaintextItself()
    {
        byte[] plaintext = [0x30, 0x7F, 0x02];

        Assert.AreSame(plaintext, KerberosAsn1.WithoutPadding(plaintext));
    }
}
